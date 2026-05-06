using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Numerics;

namespace Grape;

/// <summary>
/// A high-level renderer for drawing a scene using the GPU pipeline.
/// </summary>
internal sealed class GpuRenderer : Renderer3D, IDisposable
{
    /// <summary>
    /// Number of frames a cached mesh or texture upload may go unused
    /// before its GPU resources are evicted.
    /// </summary>
    private const int IdleEvictionFrames = 120;

    private readonly GpuDevice _device;
    private readonly List<MeshCacheEntry> _meshResources = new();
    private readonly List<TextureCacheEntry> _textureResources = new();
    private readonly Dictionary<PipelineKey, GpuPipeline> _pipelines = new();
    private readonly Dictionary<Shader, GpuShader> _stageShaders = new();
    private readonly List<DrawCommand> _commands = new();
    private GpuSampler? _defaultSampler;
    private GpuSampler? _debugTextSampler;
    private Image? _debugFontAtlas;

    // Depth target. Sized to match the swapchain image and recreated when
    // the window resizes. Used purely as scratch state during a frame so
    // overlapping triangles are resolved by camera distance rather than
    // submission order; never sampled or read by the user.
    private GpuTexture? _depthTexture;
    private uint _depthWidth;
    private uint _depthHeight;
    private const SDL.GPUTextureFormat DepthFormat = SDL.GPUTextureFormat.D32Float;
    // One vertex buffer per RenderDebugText call within a frame. Each draw
    // gets its own array so the array-keyed mesh cache resolves to a
    // distinct Mesh per draw; otherwise multiple text strings in the same
    // frame would all reference the same backing buffer and render as
    // whichever string was queued last.
    private readonly List<TextureVertex3D[]> _debugTextVertexBuffers = new();
    private readonly List<Mesh<TextureVertex3D>?> _debugTextMeshes = new();
    private int _debugTextVertexIndex;
    private long _frameNumber;

    // Per-frame state. Non-null between BeginFrame and Present.
    private GpuRenderFrame? _renderFrame;
    private GpuTexture? _colorTarget;
    private SDL.GPUTextureFormat _colorFormat;
    private SDL.FColor _clearColor;

    internal GpuRenderer(GpuDevice device)
    {
        _device = device;
    }

    /// <summary>
    /// The <see cref="GpuDevice"/> this renderer draws through.
    /// </summary>
    internal GpuDevice Device => _device;

    /// <summary>
    /// A default linear-filtered, repeating sampler used by
    /// <see cref="RenderMesh"/> when the caller does not supply one.
    /// </summary>
    internal GpuSampler DefaultSampler => _defaultSampler ??= _device.CreateSampler(new GpuSamplerCreateInfo
    {
        MinFilter = SDL.GPUFilter.Linear,
        MagFilter = SDL.GPUFilter.Linear,
        MipmapMode = SDL.GPUSamplerMipmapMode.Linear,
        AddressModeU = SDL.GPUSamplerAddressMode.Repeat,
        AddressModeV = SDL.GPUSamplerAddressMode.Repeat,
        AddressModeW = SDL.GPUSamplerAddressMode.Repeat,
    });

    internal void BeginFrame(Window3D window)
    {
        if (_renderFrame is not null)
            throw new InvalidOperationException("A frame is already in progress.");

        _frameNumber++;
        SweepCaches();
        _commands.Clear();
        _debugTextVertexIndex = 0;

        var bg = window.BackgroundColor;
        _clearColor = new SDL.FColor
        {
            R = bg.R / 255f,
            G = bg.G / 255f,
            B = bg.B / 255f,
            A = bg.A / 255f,
        };

        _colorFormat = SDL.GetGPUSwapchainTextureFormat(_device.GpuDeviceID, window.WindowId);

        _renderFrame = _device.BeginFrame();

        if (SDL.WaitAndAcquireGPUSwapchainTexture(
                _renderFrame.CommandBuffer.CommandBufferId,
                window.WindowId,
                out var swapchainTextureId,
                out var swapchainWidth,
                out var swapchainHeight) && swapchainTextureId != 0)
        {
            _colorTarget = GpuTexture.WrapBorrowed(swapchainTextureId);
            EnsureDepthTexture(swapchainWidth, swapchainHeight);
        }
        else
        {
            _colorTarget = null;
        }
    }

    /// <summary>
    /// (Re)allocates the depth texture so its size matches the current
    /// swapchain image. Disposes any previous texture when the window has
    /// been resized.
    /// </summary>
    private void EnsureDepthTexture(uint width, uint height)
    {
        if (_depthTexture is { IsDisposed: false } && width == _depthWidth && height == _depthHeight)
            return;

        _depthTexture?.Dispose();
        _depthTexture = _device.CreateTexture(new GpuTextureCreateInfo
        {
            Type = SDL.GPUTextureType.Texturetype2D,
            Format = DepthFormat,
            Usage = SDL.GPUTextureUsageFlags.DepthStencilTarget,
            Width = width,
            Height = height,
            LayerCountOrDepth = 1,
            NumLevels = 1,
            SampleCount = SDL.GPUSampleCount.SampleCount1,
        });
        _depthWidth = width;
        _depthHeight = height;
    }

    /// <summary>
    /// Draws a mesh using the given shader.
    /// </summary>
    public override void RenderMesh<TVertex>(Mesh<TVertex> mesh, ShaderSet<TVertex> shader)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        ArgumentNullException.ThrowIfNull(shader);

        _commands.Add(new DrawCommand(mesh, shader, Texture: null, Sampler: null, DepthMode, CullMode));
    }

    /// <summary>
    /// Draws a mesh using a shader that takes a typed per-draw arguments
    /// value. The bytes of <paramref name="args"/> are split across
    /// stage/slot pairs as described by
    /// <see cref="ShaderSet{TVertex,TArgs}.ArgsLayout"/>.
    /// </summary>
    public override void RenderMesh<TVertex, TArgs>(
        Mesh<TVertex> mesh,
        ShaderSet<TVertex, TArgs> shader,
        in TArgs args)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        ArgumentNullException.ThrowIfNull(shader);

        _commands.Add(new DrawCommand<TArgs>(
            mesh,
            shader,
            Texture: null,
            Sampler: null,
            DepthMode,
            CullMode,
            shader.ArgsLayout,
            args));
    }

    /// <summary>
    /// Draws a textured mesh using the given shader and image as the source texture.
    /// </summary>
    /// <remarks>
    /// The mesh and shader must both use <see cref="TextureVertex3D"/> and
    /// the mesh's vertex layout must match the shader's expected vertex layout.
    /// The image's pixels are uploaded once to a GPU texture and cached;
    /// passing the same <see cref="Image"/> instance on later frames reuses
    /// the upload.
    /// </remarks>
    public override void RenderMesh<TVertex>(
        Mesh<TVertex> mesh,
        Image texture,
        ShaderSet<TVertex> shader)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        ArgumentNullException.ThrowIfNull(shader);
        ArgumentNullException.ThrowIfNull(texture);

        _commands.Add(new DrawCommand(mesh, shader, texture, Sampler: null, DepthMode, CullMode));
    }

    /// <summary>
    /// Draws a textured mesh using a shader that takes typed per-draw args.
    /// </summary>
    public override void RenderMesh<TVertex, TArgs>(
        Mesh<TVertex> mesh,
        Image texture,
        ShaderSet<TVertex, TArgs> shader,
        in TArgs args)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        ArgumentNullException.ThrowIfNull(shader);
        ArgumentNullException.ThrowIfNull(texture);

        _commands.Add(new DrawCommand<TArgs>(
            mesh,
            shader,
            texture,
            Sampler: null,
            DepthMode,
            CullMode,
            shader.ArgsLayout,
            args));
    }

    /// <summary>
    /// Internal entry point for textured rendering with an explicit sampler.
    /// Used by <see cref="RenderDebugText"/> to pin nearest-neighbour
    /// filtering on the debug font atlas.
    /// </summary>
    internal void RenderTexturedMeshCore<TVertex>(
        Mesh<TVertex> mesh,
        Image texture,
        ShaderSet<TVertex> shader,
        GpuSampler? sampler)
        where TVertex : unmanaged
    {
        ArgumentNullException.ThrowIfNull(mesh);
        ArgumentNullException.ThrowIfNull(shader);
        ArgumentNullException.ThrowIfNull(texture);

        _commands.Add(new DrawCommand(mesh, shader, texture, sampler, DepthMode, CullMode));
    }

    internal void RenderTexturedMeshCore<TVertex, TArgs>(
        Mesh<TVertex> mesh,
        Image texture,
        ShaderSet<TVertex, TArgs> shader,
        GpuSampler? sampler,
        in TArgs args)
        where TVertex : unmanaged
        where TArgs : unmanaged
    {
        ArgumentNullException.ThrowIfNull(mesh);
        ArgumentNullException.ThrowIfNull(shader);
        ArgumentNullException.ThrowIfNull(texture);

        _commands.Add(new DrawCommand<TArgs>(
            mesh,
            shader,
            texture,
            sampler,
            DepthMode,
            CullMode,
            shader.ArgsLayout,
            args));
    }

    /// <summary>
    /// Total number of glyph cells in the debug font atlas.
    /// </summary>
    private const int DebugGlyphPixels = 8;
    private const int DebugAtlasCols = 16;
    private const int DebugAtlasRows = 8;
    private const int DebugAtlasWidth = DebugGlyphPixels * DebugAtlasCols;   // 128
    private const int DebugAtlasHeight = DebugGlyphPixels * DebugAtlasRows;  // 64

    /// <summary>
    /// Renders ASCII debug text using SDL's built-in 8x8 bitmap font, on a
    /// strip of textured quads. The font atlas is built once on first use
    /// (one quad per character, sampled from a shared 128×64 image).
    /// </summary>
    /// <param name="text">The text to render. Non-ASCII characters render
    /// as the glyph at code 0.</param>
    /// <param name="transform">World-space transform applied to the text
    /// mesh. The mesh occupies (0..text.Length) along X and (0..1) along Y
    /// in local model space; X advances one unit per character, Y goes up.</param>
    public override void RenderDebugText(string text, in Matrix4x4 transform)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.Length == 0)
            return;

        var atlas = GetDebugFontAtlas();

        // Each call within a frame needs its own backing mesh so multiple
        // strings in the same frame don't stomp each other's vertex buffer.
        // We pool both the vertex array and the Mesh<T> across frames so a
        // stable per-frame call order reuses the same GPU buffer per slot.
        int needed = text.Length * 6;
        TextureVertex3D[] verts;
        Mesh<TextureVertex3D>? mesh;
        if (_debugTextVertexIndex < _debugTextVertexBuffers.Count)
        {
            verts = _debugTextVertexBuffers[_debugTextVertexIndex];
            mesh = _debugTextMeshes[_debugTextVertexIndex];
            if (verts.Length < needed)
            {
                // Grow this slot. Drop the old mesh; its GPU buffer will
                // idle-evict via _meshResources.
                verts = new TextureVertex3D[needed];
                _debugTextVertexBuffers[_debugTextVertexIndex] = verts;
                mesh = null;
            }
        }
        else
        {
            verts = new TextureVertex3D[needed];
            _debugTextVertexBuffers.Add(verts);
            _debugTextMeshes.Add(null);
            mesh = null;
        }
        _debugTextVertexIndex++;

        const float u = 1f / DebugAtlasCols;  // uv width per glyph
        const float v = 1f / DebugAtlasRows;  // uv height per glyph

        for (int i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c >= 128) c = (char)0;
            int col = c % DebugAtlasCols;
            int row = c / DebugAtlasCols;

            float u0 = col * u, u1 = u0 + u;
            float v0 = row * v, v1 = v0 + v;

            float x0 = i;
            float x1 = i + 1;

            // Y up, baseline at 0, glyph top at 1.
            var tl = new TextureVertex3D(new Vertex3D(x0, 1f, 0f), new Vector2(u0, v0));
            var bl = new TextureVertex3D(new Vertex3D(x0, 0f, 0f), new Vector2(u0, v1));
            var tr = new TextureVertex3D(new Vertex3D(x1, 1f, 0f), new Vector2(u1, v0));
            var br = new TextureVertex3D(new Vertex3D(x1, 0f, 0f), new Vector2(u1, v1));

            int o = i * 6;
            verts[o + 0] = tl;
            verts[o + 1] = bl;
            verts[o + 2] = br;
            verts[o + 3] = tl;
            verts[o + 4] = br;
            verts[o + 5] = tr;
        }

        // Pin debug-font sampling to nearest-neighbour clamp so glyph cells
        // don't bleed across UV boundaries even with linear filtering.
        var sampler = _debugTextSampler ??= _device.CreateSampler(new GpuSamplerCreateInfo
        {
            MinFilter = SDL.GPUFilter.Nearest,
            MagFilter = SDL.GPUFilter.Nearest,
            MipmapMode = SDL.GPUSamplerMipmapMode.Nearest,
            AddressModeU = SDL.GPUSamplerAddressMode.ClampToEdge,
            AddressModeV = SDL.GPUSamplerAddressMode.ClampToEdge,
            AddressModeW = SDL.GPUSamplerAddressMode.ClampToEdge,
        });

        var span = verts.AsSpan(0, needed);
        if (mesh is null)
        {
            mesh = new Mesh<TextureVertex3D>(span, ReadOnlySpan<uint>.Empty);
            _debugTextMeshes[_debugTextVertexIndex - 1] = mesh;
        }
        else
        {
            mesh.Reset(span, ReadOnlySpan<uint>.Empty);
        }

        RenderTexturedMeshCore(
            mesh,
            atlas,
            Shaders.PositionTextureWithTransform,
            sampler,
            in transform);
    }

    private Image GetDebugFontAtlas()
    {
        if (_debugFontAtlas is { IsDisposed: false })
            return _debugFontAtlas;

        var atlas = Image.Create(DebugAtlasWidth, DebugAtlasHeight, PixelFormat.ABGR8888);

        // Drive a software renderer over the image's pixels and use SDL's
        // built-in debug-text routine to draw each ASCII glyph into its cell.
        var rendererId = SDL.CreateSoftwareRenderer(atlas._imageId);
        if (rendererId == 0)
        {
            atlas.Dispose();
            throw new InvalidOperationException(
                $"Failed to create software renderer for debug font atlas: {SDL.GetError()}");
        }

        try
        {
            // Make the background fully transparent and the glyphs white.
            SDL.SetRenderDrawColor(rendererId, 0, 0, 0, 0);
            SDL.RenderClear(rendererId);
            SDL.SetRenderDrawColor(rendererId, 255, 255, 255, 255);

            for (int code = 0; code < 128; code++)
            {
                int col = code % DebugAtlasCols;
                int row = code / DebugAtlasCols;
                SDL.RenderDebugText(
                    rendererId,
                    col * DebugGlyphPixels,
                    row * DebugGlyphPixels,
                    ((char)code).ToString());
            }

            SDL.RenderPresent(rendererId);
        }
        finally
        {
            SDL.DestroyRenderer(rendererId);
        }

        // The image's raw pixels were just written through SDL's renderer,
        // not through the version-tracked SetPixel path. Mark the image
        // dirty so any cached GPU upload re-stages on first use.
        atlas.Invalidate();

        _debugFontAtlas = atlas;
        return atlas;
    }

    /// <summary>
    /// Completes the current frame and presents it.
    /// </summary>
    public void Present()
    {
        if (_renderFrame is null)
            return;

        try
        {
            var canDraw = _colorTarget is not null && _colorFormat != SDL.GPUTextureFormat.Invalid;
            if (!canDraw)
            {
                // No swapchain image (window minimised, being torn down, etc.).
                // Submit the empty command buffer so SDL releases its resources
                // cleanly and skip the rest of the frame without throwing.
                return;
            }

            var prepared = PrepareCommands();

            if (!_renderFrame.TryBeginCopyPass(out var copyPass))
            {
                // Device is shutting down or the command buffer is no longer
                // valid; nothing useful to do this frame.
                return;
            }

            using (copyPass)
            {
                foreach (var command in prepared)
                {
                    var mesh = command.Command.Mesh;
                    var vertexBytes = mesh.GetVertexBytes();
                    var resources = command.Resources;

                    // Skip the upload entirely when the mesh hasn't changed
                    // since we last sent it to the GPU. Reset(...) on Mesh<T>
                    // bumps Version so this picks up edits automatically.
                    var needsUpload =
                        resources.VertexBuffer is null ||
                        resources.LastUploadedVersion != mesh.Version ||
                        resources.VertexBufferBytes != vertexBytes.Length;

                    if (needsUpload)
                    {
                        // Grow (never shrink) the GPU/transfer buffers so
                        // small per-frame size jitter doesn't churn allocations.
                        if (resources.VertexBuffer is null ||
                            resources.VertexBufferCapacityBytes < vertexBytes.Length)
                        {
                            resources.VertexBuffer?.Dispose();
                            resources.UploadBuffer?.Dispose();

                            resources.VertexBuffer = _device.CreateVertexBuffer<byte>(
                                (uint)vertexBytes.Length,
                                new GpuVertexBufferLayout
                                {
                                    Pitch = vertexBytes.Length / mesh.VertexCount,
                                    Elements = ImmutableArray<GpuShaderVertexElement>.Empty
                                });

                            resources.UploadBuffer = (GpuUploadBuffer)GpuUploadBuffer.Create(_device, (uint)vertexBytes.Length);
                            resources.VertexBufferCapacityBytes = vertexBytes.Length;
                        }

                        // Upload uses cycle: true under the hood so re-writing
                        // a buffer that may still be in flight is safe.
                        copyPass!.Upload(resources.UploadBuffer!, resources.VertexBuffer!, vertexBytes);
                        resources.VertexBufferBytes = vertexBytes.Length;
                        resources.LastUploadedVersion = mesh.Version;
                    }

                    if (command.Command.Texture is { } image)
                        EnsureTextureUploaded(copyPass!, image);
                }
            }

            var colorTargets = ImmutableArray.Create(new GpuColorTargetInfo
            {
                Texture = _colorTarget,
                ClearColor = _clearColor,
                LoadOp = SDL.GPULoadOp.Clear,
                StoreOp = SDL.GPUStoreOp.Store,
            });

            // Clear depth to 1.0 (the far plane in normalized device
            // coordinates) at the start of every frame and discard the
            // contents at the end -- nothing reads them after the frame.
            var depthTarget = new GpuDepthStencilTargetInfo
            {
                Texture = _depthTexture,
                ClearDepth = 1f,
                LoadOp = SDL.GPULoadOp.Clear,
                StoreOp = SDL.GPUStoreOp.DontCare,
                StencilLoadOp = SDL.GPULoadOp.DontCare,
                StencilStoreOp = SDL.GPUStoreOp.DontCare,
            };

            if (!_renderFrame.TryBeginRenderPass(
                colorTargets,
                depthTarget,
                out var renderPass))
            {
                return;
            }

            using (renderPass)
            {
                foreach (var command in prepared)
                {
                    var shader = command.Command.Shader;
                    var vsGpu = GetOrCreateGpuShader(shader.Vertex);
                    var fsGpu = GetOrCreateGpuShader(shader.Fragment);
                    var pipeline = GetOrCreatePipeline(
                        vsGpu,
                        fsGpu,
                        _colorFormat,
                        shader.VertexLayout,
                        command.Command.DepthMode,
                        command.Command.CullMode);
                    renderPass!.BindGraphicsPipeline(pipeline);
                    renderPass.BindVertexBuffers([command.Resources.VertexBuffer!]);
                    command.Command.PushArgs(renderPass);
                    if (command.Command.Texture is { } image)
                    {
                        var gpuTexture = LookupTexture(image)
                            ?? throw new InvalidOperationException(
                                "Texture upload was not recorded for this image.");
                        var sampler = command.Command.Sampler ?? DefaultSampler;
                        renderPass.BindFragmentSamplers(0,
                            [new GpuTextureSamplerBinding(gpuTexture, sampler)]);
                    }
                    renderPass.DrawPrimitives((uint)command.Command.Mesh.VertexCount);
                }
            }

            _renderFrame.Submit();
        }
        finally
        {
            _renderFrame?.Dispose();
            _renderFrame = null;
            _colorTarget = null;
            _commands.Clear();
        }
    }

    public void Dispose()
    {
        Present();
    }

    private MeshResources GetOrCreateMeshResources(Mesh mesh)
    {
        for (int i = 0; i < _meshResources.Count; i++)
        {
            var entry = _meshResources[i];
            if (entry.Mesh.TryGetTarget(out var target) && ReferenceEquals(target, mesh))
            {
                entry.LastUsedFrame = _frameNumber;
                return entry.Resources;
            }
        }

        var resources = new MeshResources();
        _meshResources.Add(new MeshCacheEntry(new WeakReference<Mesh>(mesh), resources, _frameNumber));
        return resources;
    }

    private GpuTexture? LookupTexture(Image image)
    {
        for (int i = 0; i < _textureResources.Count; i++)
        {
            var entry = _textureResources[i];
            if (entry.Image.TryGetTarget(out var target) && ReferenceEquals(target, image))
            {
                entry.LastUsedFrame = _frameNumber;
                return entry.Texture;
            }
        }

        return null;
    }

    /// <summary>
    /// Drops cached entries whose source mesh/image has been garbage
    /// collected, or that have not been used for <see cref="IdleEvictionFrames"/>
    /// frames. Their GPU buffers/textures are disposed.
    /// </summary>
    private void SweepCaches()
    {
        for (int i = _meshResources.Count - 1; i >= 0; i--)
        {
            var entry = _meshResources[i];
            var idle = _frameNumber - entry.LastUsedFrame > IdleEvictionFrames;
            var dead = !entry.Mesh.TryGetTarget(out _);
            if (idle || dead)
            {
                entry.Resources.VertexBuffer?.Dispose();
                entry.Resources.UploadBuffer?.Dispose();
                _meshResources.RemoveAt(i);
            }
        }

        for (int i = _textureResources.Count - 1; i >= 0; i--)
        {
            var entry = _textureResources[i];
            var idle = _frameNumber - entry.LastUsedFrame > IdleEvictionFrames;
            var dead = !entry.Image.TryGetTarget(out _);
            if (idle || dead)
            {
                entry.Texture.Dispose();
                entry.UploadBuffer?.Dispose();
                _textureResources.RemoveAt(i);
            }
        }
    }

    private void EnsureTextureUploaded(GpuCopyPass copyPass, Image image)
    {
        for (int i = 0; i < _textureResources.Count; i++)
        {
            var entry = _textureResources[i];
            if (entry.Image.TryGetTarget(out var target) && ReferenceEquals(target, image))
            {
                entry.LastUsedFrame = _frameNumber;

                // Re-upload only when the image's contents have changed.
                if (entry.LastUploadedVersion != image.Version)
                {
                    var (w, h) = image.Size;
                    if (w == entry.Width && h == entry.Height)
                    {
                        // Same dimensions: stream new pixels into the
                        // existing GPU texture. UploadToTexture passes
                        // cycle: true so re-writing while the previous
                        // contents may still be in flight is safe.
                        var px = image.GetPixels();
                        if (entry.UploadBuffer is null || entry.UploadBufferBytes < px.Length)
                        {
                            entry.UploadBuffer?.Dispose();
                            entry.UploadBuffer = (GpuUploadBuffer)GpuUploadBuffer.Create(_device, (uint)px.Length);
                            entry.UploadBufferBytes = px.Length;
                        }
                        copyPass.UploadToTexture(entry.UploadBuffer!, entry.Texture, (uint)w, (uint)h, px);
                    }
                    else
                    {
                        // Image was resized (rare): rebuild the texture.
                        entry.Texture.Dispose();
                        entry.UploadBuffer?.Dispose();
                        entry.UploadBuffer = null;
                        entry.UploadBufferBytes = 0;

                        var resizedFormat = MapPixelFormat(image.PixelFormat);
                        entry.Texture = _device.CreateTexture(new GpuTextureCreateInfo
                        {
                            Type = SDL.GPUTextureType.Texturetype2D,
                            Format = resizedFormat,
                            Usage = SDL.GPUTextureUsageFlags.Sampler,
                            Width = (uint)w,
                            Height = (uint)h,
                            LayerCountOrDepth = 1,
                            NumLevels = 1,
                            SampleCount = SDL.GPUSampleCount.SampleCount1,
                        });
                        entry.Width = w;
                        entry.Height = h;

                        var px = image.GetPixels();
                        using var upload = (GpuUploadBuffer)GpuUploadBuffer.Create(_device, (uint)px.Length);
                        copyPass.UploadToTexture(upload, entry.Texture, (uint)w, (uint)h, px);
                    }

                    entry.LastUploadedVersion = image.Version;
                }
                return;
            }
        }

        var (width, height) = image.Size;
        var format = MapPixelFormat(image.PixelFormat);

        var gpuTexture = _device.CreateTexture(new GpuTextureCreateInfo
        {
            Type = SDL.GPUTextureType.Texturetype2D,
            Format = format,
            Usage = SDL.GPUTextureUsageFlags.Sampler,
            Width = (uint)width,
            Height = (uint)height,
            LayerCountOrDepth = 1,
            NumLevels = 1,
            SampleCount = SDL.GPUSampleCount.SampleCount1,
        });

        var pixels = image.GetPixels();
        using var firstUpload = (GpuUploadBuffer)GpuUploadBuffer.Create(_device, (uint)pixels.Length);
        copyPass.UploadToTexture(firstUpload, gpuTexture, (uint)width, (uint)height, pixels);

        _textureResources.Add(new TextureCacheEntry(
            new WeakReference<Image>(image), gpuTexture, _frameNumber)
        {
            Width = width,
            Height = height,
            LastUploadedVersion = image.Version,
        });
    }

    private static SDL.GPUTextureFormat MapPixelFormat(PixelFormat format) => format switch
    {
        // SDL_PIXELFORMAT_ABGR8888 stores bytes in memory as R, G, B, A on
        // little-endian platforms, matching SDL_GPU R8G8B8A8_UNORM.
        PixelFormat.ABGR8888 => SDL.GPUTextureFormat.R8G8B8A8Unorm,
        PixelFormat.ARGB8888 => SDL.GPUTextureFormat.B8G8R8A8Unorm,
        _ => throw new NotSupportedException(
            $"Image pixel format '{format}' has no GPU texture format mapping. " +
            "Convert the image to ABGR8888 before sampling on the GPU."),
    };

    private GpuPipeline GetOrCreatePipeline(
        GpuShader vertexShader,
        GpuShader fragmentShader,
        SDL.GPUTextureFormat colorFormat,
        ShaderVertexLayout layout,
        DepthMode depthMode,
        CullMode cullMode)
    {
        var key = new PipelineKey(vertexShader, fragmentShader, colorFormat, layout, depthMode, cullMode);
        if (!_pipelines.TryGetValue(key, out var pipeline))
        {
            pipeline = CreatePipeline(vertexShader, fragmentShader, colorFormat, layout, depthMode, cullMode);
            _pipelines[key] = pipeline;
        }

        return pipeline;
    }

    private GpuShader GetOrCreateGpuShader(Shader stage)
    {
        if (_stageShaders.TryGetValue(stage, out var existing))
            return existing;

        var format = SelectShaderFormat(_device.ShaderFormat);
        var code = stage.GetCode(format);
        var resources = stage.GetResources();
        var created = _device.CreateShader(new GpuShaderCreateInfo
        {
            Code = code,
            Entrypoint = stage.Entrypoint,
            Format = MapShaderFormat(format),
            Stage = stage.Kind == ShaderKind.Vertex
                ? SDL.GPUShaderStage.Vertex
                : SDL.GPUShaderStage.Fragment,
            NumSamplers = resources.NumSamplers,
            NumUniformBuffers = resources.NumUniformBuffers,
            NumStorageTextures = resources.NumStorageTextures,
            NumStorageBuffers = resources.NumStorageBuffers,
        });
        _stageShaders[stage] = created;
        return created;
    }

    /// <summary>
    /// Picks one concrete <see cref="ShaderFormat"/> from the formats the
    /// GPU device reports it accepts. Vulkan/SPIR-V is preferred when
    /// available, then DXIL on D3D12, then MSL on Metal -- the same priority
    /// order Grape's built-in shader loader uses.
    /// </summary>
    private static ShaderFormat SelectShaderFormat(SDL.GPUShaderFormat supported)
    {
        if ((supported & SDL.GPUShaderFormat.SPIRV) != 0) return ShaderFormat.Spirv;
        if ((supported & SDL.GPUShaderFormat.DXIL)  != 0) return ShaderFormat.Dxil;
        if ((supported & SDL.GPUShaderFormat.MSL)   != 0) return ShaderFormat.Msl;
        throw new NotSupportedException(
            $"GPU device reports no shader format Grape can produce. Reported: {supported}.");
    }

    private static SDL.GPUShaderFormat MapShaderFormat(ShaderFormat format) => format switch
    {
        ShaderFormat.Spirv => SDL.GPUShaderFormat.SPIRV,
        ShaderFormat.Dxil  => SDL.GPUShaderFormat.DXIL,
        ShaderFormat.Msl   => SDL.GPUShaderFormat.MSL,
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, null),
    };

    private GpuPipeline CreatePipeline(
        GpuShader vertexShader,
        GpuShader fragmentShader,
        SDL.GPUTextureFormat colorFormat,
        ShaderVertexLayout layout,
        DepthMode depthMode,
        CullMode cullMode)
    {
        var attributes = ImmutableArray.CreateBuilder<SDL.GPUVertexAttribute>(layout.Elements.Length);
        uint offset = 0;
        for (var i = 0; i < layout.Elements.Length; i++)
        {
            var element = layout.Elements[i];
            var (format, size) = MapShaderVertexElement(element.Kind);
            attributes.Add(new SDL.GPUVertexAttribute
            {
                Location = (uint)i,
                BufferSlot = 0,
                Format = format,
                Offset = offset,
            });
            offset += size;
        }

        var bufferDescriptions = ImmutableArray.Create(new SDL.GPUVertexBufferDescription
        {
            Slot = 0,
            Pitch = offset,
            InputRate = SDL.GPUVertexInputRate.Vertex,
            InstanceStepRate = 0,
        });

        var colorTargets = ImmutableArray.Create(new SDL.GPUColorTargetDescription
        {
            Format = colorFormat,
            BlendState = new SDL.GPUColorTargetBlendState
            {
                EnableBlend = 1,
                SrcColorBlendfactor = SDL.GPUBlendFactor.SrcAlpha,
                DstColorBlendfactor = SDL.GPUBlendFactor.OneMinusSrcAlpha,
                ColorBlendOp = SDL.GPUBlendOp.Add,
                SrcAlphaBlendfactor = SDL.GPUBlendFactor.One,
                DstAlphaBlendfactor = SDL.GPUBlendFactor.OneMinusSrcAlpha,
                AlphaBlendOp = SDL.GPUBlendOp.Add,
            },
        });

        // Map the user-facing DepthMode to the two GPU switches it
        // controls. Default = standard 3D occlusion. Transparent reads
        // the depth buffer (so solid geometry occludes us) but doesn't
        // write to it (so other transparent draws still see what's
        // behind us). Overlay ignores depth entirely -- always draws,
        // never occludes, never gets occluded.
        var (enableTest, enableWrite) = depthMode switch
        {
            DepthMode.Solid       => ((byte)1, (byte)1),
            DepthMode.Transparent => ((byte)1, (byte)0),
            DepthMode.Overlay     => ((byte)0, (byte)0),
            _ => throw new ArgumentOutOfRangeException(nameof(depthMode), depthMode, null),
        };

        var depthState = new SDL.GPUDepthStencilState
        {
            CompareOp = SDL.GPUCompareOp.Less,
            EnableDepthTest = enableTest,
            EnableDepthWrite = enableWrite,
        };

        // CCW winding identifies the "front" face -- the convention used
        // by every common 3D content tool. Cull mode then decides whether
        // to draw both sides, only fronts, or only backs.
        var rasterizerState = new SDL.GPURasterizerState
        {
            FillMode = SDL.GPUFillMode.Fill,
            CullMode = cullMode switch
            {
                CullMode.None  => SDL.GPUCullMode.None,
                CullMode.Back  => SDL.GPUCullMode.Back,
                CullMode.Front => SDL.GPUCullMode.Front,
                _ => throw new ArgumentOutOfRangeException(nameof(cullMode), cullMode, null),
            },
            FrontFace = SDL.GPUFrontFace.CounterClockwise,
        };

        return _device.CreateGraphicsPipeline(new GpuPipelineCreateInfo
        {
            VertexShader = vertexShader,
            FragmentShader = fragmentShader,
            VertexInputState = new GpuVertexInputState
            {
                BufferDescriptions = bufferDescriptions,
                Attributes = attributes.ToImmutable(),
            },
            PrimitiveType = SDL.GPUPrimitiveType.TriangleList,
            RasterizerState = rasterizerState,
            DepthStencilState = depthState,
            TargetInfo = new GpuPipelineTargetInfo
            {
                ColorTargetDescriptions = colorTargets,
                DepthStencilFormat = DepthFormat,
                HasDepthStencilTarget = true,
            },
        });
    }

    private static (SDL.GPUVertexElementFormat Format, uint Size) MapShaderVertexElement(ShaderVertexElementKind kind) => kind switch
    {
        ShaderVertexElementKind.Position3 => (SDL.GPUVertexElementFormat.Float3, 12u),
        ShaderVertexElementKind.TextureCoordinate2 => (SDL.GPUVertexElementFormat.Float2, 8u),
        ShaderVertexElementKind.Color4 => (SDL.GPUVertexElementFormat.Ubyte4Norm, 4u),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    private readonly record struct PipelineKey(
        GpuShader VertexShader,
        GpuShader FragmentShader,
        SDL.GPUTextureFormat ColorFormat,
        ShaderVertexLayout Layout,
        DepthMode DepthMode,
        CullMode CullMode);

    private record DrawCommand(
        Mesh Mesh,
        ShaderSet Shader,
        Image? Texture,
        GpuSampler? Sampler,
        DepthMode DepthMode,
        CullMode CullMode)
    {
        /// <summary>
        /// Pushes any per-draw arguments this command carries to the given
        /// render pass. The base command has no args; the generic
        /// <see cref="DrawCommand{TArgs}"/> subtype overrides this to push
        /// the slots described by its <see cref="ShaderArgsLayout"/>.
        /// </summary>
        public virtual void PushArgs(GpuRenderPass renderPass) { }
    }

    private sealed record DrawCommand<TArgs>(
        Mesh Mesh,
        ShaderSet Shader,
        Image? Texture,
        GpuSampler? Sampler,
        DepthMode DepthMode,
        CullMode CullMode,
        ShaderArgsLayout Layout,
        TArgs Args)
        : DrawCommand(Mesh, Shader, Texture, Sampler, DepthMode, CullMode)
        where TArgs : unmanaged
    {
        public override void PushArgs(GpuRenderPass renderPass)
        {
            // Read the typed value into a local so we have a stable ref
            // for the span; this avoids any heap allocation per draw.
            //
            // Note for Matrix4x4 args: System.Numerics.Matrix4x4 is row-major
            // in memory while HLSL reads cbuffer matrices column-major by
            // default. The two interpretations cancel out: pushing the
            // System.Numerics matrix raw produces the same transformation
            // HLSL's mul(M, v) does for a column-vector v that csharp's
            // `v * M` does for a row-vector v. So no transpose is needed.
            var value = Args;
            var bytes = MemoryMarshal.AsBytes(
                MemoryMarshal.CreateReadOnlySpan(ref value, 1));
            int offset = 0;
            foreach (var element in Layout.Elements)
            {
                var slice = bytes.Slice(offset, element.Size);
                switch (element.Stage)
                {
                    case ShaderArgStage.Vertex:
                        renderPass.PushVertexUniformData((uint)element.Slot, slice);
                        break;
                    case ShaderArgStage.Fragment:
                        renderPass.PushFragmentUniformData((uint)element.Slot, slice);
                        break;
                }
                offset += element.Size;
            }
        }
    }

    private sealed record PreparedDrawCommand(
        DrawCommand Command,
        MeshResources Resources);

    private sealed class MeshResources
    {
        public GpuVertexBuffer? VertexBuffer { get; set; }
        public GpuUploadBuffer? UploadBuffer { get; set; }
        public int VertexBufferBytes { get; set; }
        public int VertexBufferCapacityBytes { get; set; }
        public int LastUploadedVersion { get; set; }
    }

    private sealed class MeshCacheEntry
    {
        public MeshCacheEntry(WeakReference<Mesh> mesh, MeshResources resources, long lastUsedFrame)
        {
            Mesh = mesh;
            Resources = resources;
            LastUsedFrame = lastUsedFrame;
        }

        public WeakReference<Mesh> Mesh { get; }
        public MeshResources Resources { get; }
        public long LastUsedFrame { get; set; }
    }

    private sealed class TextureCacheEntry
    {
        public TextureCacheEntry(WeakReference<Image> image, GpuTexture texture, long lastUsedFrame)
        {
            Image = image;
            Texture = texture;
            LastUsedFrame = lastUsedFrame;
        }

        public WeakReference<Image> Image { get; }
        public GpuTexture Texture { get; set; }
        public long LastUsedFrame { get; set; }
        public int LastUploadedVersion { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public GpuUploadBuffer? UploadBuffer { get; set; }
        public int UploadBufferBytes { get; set; }
    }

    private List<PreparedDrawCommand> PrepareCommands()
    {
        var prepared = new List<PreparedDrawCommand>(_commands.Count);

        foreach (var command in _commands)
        {
            prepared.Add(new PreparedDrawCommand(
                command,
                GetOrCreateMeshResources(command.Mesh)));
        }

        return prepared;
    }
}
