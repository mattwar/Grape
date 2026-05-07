using System.Buffers;
using System.Collections.Immutable;
using System.Runtime.InteropServices;
using System.Numerics;

namespace Grape;

/// <summary>
/// A high-level renderer for drawing a scene using the GPU pipeline.
/// </summary>
internal class GpuRenderer : Renderer3D, IDisposable
{
    /// <summary>
    /// Number of frames a cached mesh or texture upload may go unused
    /// before its GPU resources are evicted.
    /// </summary>
    private const int IdleEvictionFrames = 120;

    private readonly GpuDevice _device;
    private readonly Window3D? _window;
    private readonly List<MeshCacheEntry> _meshResources = new();
    private readonly List<TextureCacheEntry> _textureResources = new();
    private readonly Dictionary<PipelineKey, GpuPipeline> _pipelines = new();
    private readonly Dictionary<Shader, GpuShader> _stageShaders = new();
    private readonly List<DrawCommand> _commands = new();

    // Pool of instance-rate vertex buffers + their staging upload buffers.
    // Each instanced DrawCommand acquires one for the frame; at frame end
    // they all return to the pool. Buffers grow monotonically and are
    // recycled across frames; SDL's Upload uses cycle:true so re-writing
    // a buffer that may still be in flight is safe.
    private readonly List<InstanceBuffer> _instanceBufferPool = new();
    private int _instanceBuffersAcquired;
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
    // One vertex buffer per DrawDebugText call within a frame. Each draw
    // gets its own array so the array-keyed mesh cache resolves to a
    // distinct Mesh per draw; otherwise multiple text strings in the same
    // frame would all reference the same backing buffer and render as
    // whichever string was queued last.
    private readonly List<TextureVertex3D[]> _debugTextVertexBuffers = new();
    private readonly List<Mesh<TextureVertex3D>?> _debugTextMeshes = new();
    private int _debugTextVertexIndex;
    private long _frameNumber;

    // Per-frame state. Non-null between BeginFrame and Render.
    private GpuRenderFrame? _renderFrame;
    private GpuTexture? _colorTarget;
    private SDL.GPUTextureFormat _colorFormat;
    private SDL.FColor _clearColor;

    internal GpuRenderer(GpuDevice device, Window3D window)
    {
        _device = device;
        _window = window;
        ApplySyncMode(base.SyncMode);
    }

    /// <summary>
    /// Constructor for subclasses that own their own color target (no
    /// window/swapchain). They must override <see cref="TryAcquireColorTarget"/>
    /// and <see cref="PresentFrame"/>; <see cref="ApplySyncMode"/> becomes a
    /// no-op (sync mode is meaningful only for swapchain presentation).
    /// </summary>
    private protected GpuRenderer(GpuDevice device)
    {
        _device = device;
        _window = null;
    }

    /// <summary>
    /// The <see cref="GpuDevice"/> this renderer draws through.
    /// </summary>
    internal GpuDevice Device => _device;

    /// <inheritdoc/>
    public override SyncMode SyncMode
    {
        get => base.SyncMode;
        set
        {
            base.SyncMode = value;
            ApplySyncMode(value);
        }
    }

    private void ApplySyncMode(SyncMode mode)
    {
        if (_window is null)
            return;

        // Map the requested mode to the closest supported SDL_GPU
        // present mode. WaitForSync (FIFO) is always supported, so it's
        // the universal fallback. Hint -> first supported in priority
        // order: Latest -> Mailbox, Immediate, VSync; Immediate ->
        // Immediate, VSync; WaitForSync -> VSync.
        var preferences = mode switch
        {
            SyncMode.Latest => new[] { SDL.GPUPresentMode.Mailbox, SDL.GPUPresentMode.Immediate, SDL.GPUPresentMode.VSync },
            SyncMode.Immediate => new[] { SDL.GPUPresentMode.Immediate, SDL.GPUPresentMode.VSync },
            _ => new[] { SDL.GPUPresentMode.VSync },
        };

        var deviceId = _device.GpuDeviceID;
        var windowId = _window.WindowId;
        if (deviceId == 0 || windowId == 0)
            return;

        SDL.GPUPresentMode chosen = SDL.GPUPresentMode.VSync;
        foreach (var candidate in preferences)
        {
            if (candidate == SDL.GPUPresentMode.VSync ||
                SDL.WindowSupportsGPUPresentMode(deviceId, windowId, candidate))
            {
                chosen = candidate;
                break;
            }
        }

        // Composition stays at SDL's default (SDR); we only adjust the
        // present mode here.
        SDL.SetGPUSwapchainParameters(
            deviceId,
            windowId,
            SDL.GPUSwapchainComposition.SDR,
            chosen);
    }

    /// <summary>
    /// A default linear-filtered, repeating sampler used by
    /// <see cref="DrawMesh"/> when the caller does not supply one.
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

    /// <summary>
    /// Acquires the swapchain image for the next frame and stages
    /// per-frame state. Called at the top of <see cref="Render"/>.
    /// </summary>
    private void BeginFrame()
    {
        var bg = BackgroundColor;
        _clearColor = new SDL.FColor
        {
            R = bg.R / 255f,
            G = bg.G / 255f,
            B = bg.B / 255f,
            A = bg.A / 255f,
        };

        _renderFrame = _device.BeginFrame();

        if (TryAcquireColorTarget(_renderFrame, out _colorTarget, out _colorFormat, out var width, out var height))
        {
            EnsureDepthTexture(width, height);
        }
        else
        {
            _colorTarget = null;
        }
    }

    /// <summary>
    /// Resolves the color target for the current frame. The default
    /// implementation acquires the next swapchain image of the bound
    /// window. Subclasses that own their target (e.g. an image-bound
    /// renderer) override this to return their owned texture.
    /// </summary>
    protected virtual bool TryAcquireColorTarget(
        GpuRenderFrame frame,
        out GpuTexture? colorTarget,
        out SDL.GPUTextureFormat colorFormat,
        out uint width,
        out uint height)
    {
        colorTarget = null;
        colorFormat = SDL.GPUTextureFormat.Invalid;
        width = 0;
        height = 0;

        if (_window is null)
            return false;

        colorFormat = SDL.GetGPUSwapchainTextureFormat(_device.GpuDeviceID, _window.WindowId);

        if (SDL.WaitAndAcquireGPUSwapchainTexture(
                frame.CommandBuffer.CommandBufferId,
                _window.WindowId,
                out var swapchainTextureId,
                out width,
                out height) && swapchainTextureId != 0)
        {
            colorTarget = GpuTexture.WrapBorrowed(swapchainTextureId);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Finalises the current frame's command buffer. The default just
    /// submits it (the swapchain present is implied). Subclasses that
    /// render into their own target override this to also queue a
    /// download copy pass and read pixels back to the CPU.
    /// </summary>
    protected virtual void PresentFrame()
    {
        _renderFrame!.Submit();
    }

    /// <summary>
    /// Hook invoked at the start of the frame's main copy pass, before
    /// any mesh/texture uploads. The default does nothing. Subclasses
    /// override this to stage extra uploads into their target -- for
    /// example, copying an image's existing CPU pixels into the GPU
    /// color target so subsequent draws compose on top of them.
    /// </summary>
    protected virtual void OnBeforeUploads(GpuCopyPass copyPass)
    {
    }

    /// <summary>The frame currently being recorded, or null between frames.</summary>
    private protected GpuRenderFrame? CurrentFrame => _renderFrame;

    /// <summary>The color target for the current frame, or null if acquisition failed.</summary>
    private protected GpuTexture? CurrentColorTarget => _colorTarget;

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
    /// Queues a mesh for drawing using the given shader.
    /// </summary>
    public override void DrawMesh<TVertex>(Mesh<TVertex> mesh, ShaderSet<TVertex> shader)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        ArgumentNullException.ThrowIfNull(shader);

        var (topology, wireframe) = ResolveDrawState(mesh.Topology);
        _commands.Add(new DrawCommand(mesh, shader, Texture: null, Sampler: null, DepthMode, BlendMode, CullMode, topology, wireframe, Viewport, ClipRect));
    }

    /// <summary>
    /// Queues a mesh for drawing using a shader that takes a typed per-draw arguments
    /// value. The bytes of <paramref name="args"/> are split across
    /// stage/slot pairs as described by
    /// <see cref="ShaderSet{TVertex,TArgs}.ArgsLayout"/>.
    /// </summary>
    public override void DrawMesh<TVertex, TArgs>(
        Mesh<TVertex> mesh,
        ShaderSet<TVertex, TArgs> shader,
        in TArgs args)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        ArgumentNullException.ThrowIfNull(shader);

        var (topology, wireframe) = ResolveDrawState(mesh.Topology);
        _commands.Add(new DrawCommand<TArgs>(
            mesh,
            shader,
            Texture: null,
            Sampler: null,
            DepthMode,
            BlendMode,
            CullMode,
            topology,
            wireframe,
            Viewport,
            ClipRect,
            shader.ArgsLayout,
            args));
    }

    /// <summary>
    /// Queues a textured mesh for drawing using the given shader and image as the source texture.
    /// </summary>
    /// <remarks>
    /// The mesh and shader must both use <see cref="TextureVertex3D"/> and
    /// the mesh's vertex layout must match the shader's expected vertex layout.
    /// The image's pixels are uploaded once to a GPU texture and cached;
    /// passing the same <see cref="Image"/> instance on later frames reuses
    /// the upload.
    /// </remarks>
    public override void DrawMesh<TVertex>(
        Mesh<TVertex> mesh,
        Image texture,
        ShaderSet<TVertex> shader)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        ArgumentNullException.ThrowIfNull(shader);
        ArgumentNullException.ThrowIfNull(texture);

        var (topology, wireframe) = ResolveDrawState(mesh.Topology);
        _commands.Add(new DrawCommand(mesh, shader, texture, Sampler: null, DepthMode, BlendMode, CullMode, topology, wireframe, Viewport, ClipRect));
    }

    /// <summary>
    /// Queues a textured mesh for drawing using a shader that takes typed per-draw args.
    /// </summary>
    public override void DrawMesh<TVertex, TArgs>(
        Mesh<TVertex> mesh,
        Image texture,
        ShaderSet<TVertex, TArgs> shader,
        in TArgs args)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        ArgumentNullException.ThrowIfNull(shader);
        ArgumentNullException.ThrowIfNull(texture);

        var (topology, wireframe) = ResolveDrawState(mesh.Topology);
        _commands.Add(new DrawCommand<TArgs>(
            mesh,
            shader,
            texture,
            Sampler: null,
            DepthMode,
            BlendMode,
            CullMode,
            topology,
            wireframe,
            Viewport,
            ClipRect,
            shader.ArgsLayout,
            args));
    }

    /// <summary>
    /// Internal entry point for textured rendering with an explicit sampler.
    /// Used by <see cref="DrawDebugText"/> to pin nearest-neighbour
    /// filtering on the debug font atlas.
    /// </summary>
    internal void DrawTexturedMeshCore<TVertex>(
        Mesh<TVertex> mesh,
        Image texture,
        ShaderSet<TVertex> shader,
        GpuSampler? sampler)
        where TVertex : unmanaged
    {
        ArgumentNullException.ThrowIfNull(mesh);
        ArgumentNullException.ThrowIfNull(shader);
        ArgumentNullException.ThrowIfNull(texture);

        var (topology, wireframe) = ResolveDrawState(mesh.Topology);
        _commands.Add(new DrawCommand(mesh, shader, texture, sampler, DepthMode, BlendMode, CullMode, topology, wireframe, Viewport, ClipRect));
    }

    internal void DrawTexturedMeshCore<TVertex, TArgs>(
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

        var (topology, wireframe) = ResolveDrawState(mesh.Topology);
        _commands.Add(new DrawCommand<TArgs>(
            mesh,
            shader,
            texture,
            sampler,
            DepthMode,
            BlendMode,
            CullMode,
            topology,
            wireframe,
            Viewport,
            ClipRect,
            shader.ArgsLayout,
            args));
    }

    /// <inheritdoc/>
    public override void DrawMesh<TVertex, TArgs, TInstance>(
        Mesh<TVertex> mesh,
        InstancedShaderSet<TVertex, TArgs, TInstance> shader,
        in TArgs args,
        ReadOnlySpan<TInstance> instances)
    {
        QueueInstanced(mesh, texture: null, shader, args, instances);
    }

    /// <inheritdoc/>
    public override void DrawMesh<TVertex, TArgs, TInstance>(
        Mesh<TVertex> mesh,
        Image texture,
        InstancedShaderSet<TVertex, TArgs, TInstance> shader,
        in TArgs args,
        ReadOnlySpan<TInstance> instances)
    {
        ArgumentNullException.ThrowIfNull(texture);
        QueueInstanced(mesh, texture, shader, args, instances);
    }

    private void QueueInstanced<TVertex, TArgs, TInstance>(
        Mesh<TVertex> mesh,
        Image? texture,
        InstancedShaderSet<TVertex, TArgs, TInstance> shader,
        in TArgs args,
        ReadOnlySpan<TInstance> instances)
        where TVertex : unmanaged
        where TArgs : unmanaged
        where TInstance : unmanaged
    {
        ArgumentNullException.ThrowIfNull(mesh);
        ArgumentNullException.ThrowIfNull(shader);

        if (instances.IsEmpty)
            return;

        // Snapshot the instance bytes into a pooled byte[] so the user's
        // span doesn't have to outlive the call. The array is returned to
        // ArrayPool at frame end.
        var bytes = MemoryMarshal.AsBytes(instances);
        var rented = ArrayPool<byte>.Shared.Rent(bytes.Length);
        bytes.CopyTo(rented);

        // Per-instance Matrix4x4 attributes are reconstructed in HLSL with
        // consecutive locations as rows, so the shader sees them in their
        // native layout. Per-uniform cbuffer matrices, by contrast, are
        // implicitly transposed by HLSL's column-major cbuffer storage. To
        // keep the shaders symmetric (`mul(M, v)` everywhere), we transpose
        // each per-instance matrix here so both paths arrive at the shader
        // with the same orientation.
        TransposeInstanceMatrices(rented.AsSpan(0, bytes.Length), shader.InstanceLayout, instances.Length);

        var (topology, wireframe) = ResolveDrawState(mesh.Topology);
        _commands.Add(new InstancedDrawCommand<TArgs>(
            mesh,
            shader,
            texture,
            Sampler: null,
            DepthMode,
            BlendMode,
            CullMode,
            topology,
            wireframe,
            Viewport,
            ClipRect,
            shader.ArgsLayout,
            args,
            shader.InstanceLayout,
            rented,
            bytes.Length,
            instances.Length));
    }

    // Combines the mesh's declared topology with the renderer's
    // current Wireframe flag. When wireframe is on AND the mesh is
    // triangle-based, the effective topology becomes LineList (drawn
    // through a derived edge-index buffer, see Present's copy pass).
    // For non-triangle meshes the flag is silently ignored -- they're
    // already line/point shaped and don't need conversion.
    private (Topology Topology, bool Wireframe) ResolveDrawState(Topology meshTopology)
    {
        if (Wireframe && (meshTopology == Topology.TriangleList || meshTopology == Topology.TriangleStrip))
            return (Topology.LineList, true);
        return (meshTopology, false);
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
    public override void DrawDebugText(string text, in Matrix4x4 transform)
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

        DrawTexturedMeshCore(
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
    /// Completes the current frame: encodes all queued draws and presents the result.
    /// </summary>
    protected override void RenderOnApplicationThread()
    {
        // Snapshot the frame clock at the START of the render so the next
        // handler's ElapsedSinceLastRender reflects the full frame interval
        // (including this render's own work), not just the gap between
        // present and the next handler invocation.
        AdvanceFrameClock();
        BeginFrame();

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
                OnBeforeUploads(copyPass!);

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

                    // Index buffer mirrors the vertex-buffer flow: the
                    // same Version controls re-upload, capacity grows
                    // monotonically, and meshes without indices skip
                    // this whole branch and fall through to unindexed
                    // drawing later.
                    if (mesh.IndexCount > 0)
                    {
                        var indexBytes = MemoryMarshal.AsBytes(mesh.GetIndices());
                        if (needsUpload || resources.IndexBuffer is null || resources.IndexBufferBytes != indexBytes.Length)
                        {
                            if (resources.IndexBuffer is null ||
                                resources.IndexBufferCapacityBytes < indexBytes.Length)
                            {
                                resources.IndexBuffer?.Dispose();
                                resources.IndexUploadBuffer?.Dispose();

                                resources.IndexBuffer = _device.CreateIndexBuffer((uint)indexBytes.Length);
                                resources.IndexUploadBuffer = (GpuUploadBuffer)GpuUploadBuffer.Create(_device, (uint)indexBytes.Length);
                                resources.IndexBufferCapacityBytes = indexBytes.Length;
                            }

                            copyPass!.Upload(resources.IndexUploadBuffer!, resources.IndexBuffer!, indexBytes);
                            resources.IndexBufferBytes = indexBytes.Length;
                        }
                    }

                    // Wireframe edge-index buffer: derived from the
                    // mesh's triangle indices (or vertex order if
                    // unindexed). Built once per mesh.Version change
                    // and reused across frames. Cost is paid only for
                    // meshes that are actually drawn in wireframe.
                    if (command.Command.Wireframe &&
                        resources.LastWireframeUploadedVersion != mesh.Version)
                    {
                        var edges = BuildWireframeIndices(
                            mesh.GetIndices(),
                            mesh.VertexCount,
                            mesh.Topology);
                        var edgeBytes = MemoryMarshal.AsBytes(edges.AsSpan());

                        if (resources.WireframeIndexBuffer is null ||
                            resources.WireframeIndexBufferCapacityBytes < edgeBytes.Length)
                        {
                            resources.WireframeIndexBuffer?.Dispose();
                            resources.WireframeIndexUploadBuffer?.Dispose();

                            resources.WireframeIndexBuffer = _device.CreateIndexBuffer((uint)edgeBytes.Length);
                            resources.WireframeIndexUploadBuffer = (GpuUploadBuffer)GpuUploadBuffer.Create(_device, (uint)edgeBytes.Length);
                            resources.WireframeIndexBufferCapacityBytes = edgeBytes.Length;
                        }

                        copyPass!.Upload(
                            resources.WireframeIndexUploadBuffer!,
                            resources.WireframeIndexBuffer!,
                            edgeBytes);
                        resources.WireframeIndexBufferBytes = edgeBytes.Length;
                        resources.WireframeIndexCount = edges.Length;
                        resources.LastWireframeUploadedVersion = mesh.Version;
                    }

                    if (command.Command.Texture is { } image)
                        EnsureTextureUploaded(copyPass!, image);

                    // Per-instance vertex data (slot 1) for instanced
                    // commands. Acquired from a pool that grows monotonically
                    // and is recycled across frames; the user's instance
                    // bytes were already snapshotted into a pooled byte[]
                    // at queue time.
                    var instanceBytes = command.Command.InstanceBytes;
                    if (!instanceBytes.IsEmpty)
                    {
                        var instance = AcquireInstanceBuffer(instanceBytes.Length);
                        copyPass!.Upload(instance.Upload!, instance.Buffer!, instanceBytes);
                        command.Instance = instance;
                    }
                }
            }

            var colorTargets = ImmutableArray.Create(new GpuColorTargetInfo
            {
                Texture = _colorTarget,
                ClearColor = _clearColor,
                LoadOp = AutoClear ? SDL.GPULoadOp.Clear : SDL.GPULoadOp.Load,
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
                // The render pass starts with viewport/scissor implicitly
                // covering the whole render target. We track the last
                // values we explicitly applied so consecutive draws with
                // the same setting don't emit redundant SDL calls.
                Rect? lastViewport = null;
                Rect? lastClipRect = null;
                bool viewportApplied = false;
                bool clipApplied = false;
                var fullW = _depthWidth;
                var fullH = _depthHeight;

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
                        command.Command.InstanceLayout,
                        command.Command.DepthMode,
                        command.Command.BlendMode,
                        command.Command.CullMode,
                        command.Command.Topology);
                    renderPass!.BindGraphicsPipeline(pipeline);
                    if (command.Instance is { Buffer: not null } instance)
                    {
                        renderPass.BindVertexBuffers(
                            [command.Resources.VertexBuffer!, instance.Buffer]);
                    }
                    else
                    {
                        renderPass.BindVertexBuffers([command.Resources.VertexBuffer!]);
                    }

                    // Apply viewport/scissor only on transitions: the
                    // first user-set value and any change after.
                    // Switching back to "null" (full target) requires
                    // an explicit reset call once we've ever applied a
                    // user value -- otherwise SDL keeps the previous.
                    var v = command.Command.Viewport;
                    if (!viewportApplied && v is not null)
                    {
                        ApplyViewport(renderPass, v.Value, fullW, fullH);
                        viewportApplied = true;
                        lastViewport = v;
                    }
                    else if (viewportApplied && v != lastViewport)
                    {
                        ApplyViewport(renderPass, v ?? new Rect(0, 0, fullW, fullH), fullW, fullH);
                        lastViewport = v;
                    }

                    var c = command.Command.ClipRect;
                    if (!clipApplied && c is not null)
                    {
                        ApplyClip(renderPass, c.Value, fullW, fullH);
                        clipApplied = true;
                        lastClipRect = c;
                    }
                    else if (clipApplied && c != lastClipRect)
                    {
                        ApplyClip(renderPass, c ?? new Rect(0, 0, fullW, fullH), fullW, fullH);
                        lastClipRect = c;
                    }

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

                    // Wireframe takes precedence over the mesh's native
                    // index/unindexed paths: it always draws indexed,
                    // through the derived edge buffer above.
                    var instanceCount = (uint)command.Command.InstanceCount;
                    if (command.Command.Wireframe)
                    {
                        renderPass.BindIndexBuffer(
                            command.Resources.WireframeIndexBuffer!,
                            SDL.GPUIndexElementSize.IndexElementSize32Bit);
                        renderPass.DrawIndexedPrimitives(
                            (uint)command.Resources.WireframeIndexCount,
                            instanceCount);
                    }
                    else
                    {
                        // Indexed vs unindexed draw: indexed lets the GPU
                        // reuse vertex-shader results across triangles that
                        // share a vertex, and lets the mesh data store each
                        // unique vertex only once. Mesh<T> with an empty
                        // index span falls through to the original
                        // DrawPrimitives path.
                        var indexCount = command.Command.Mesh.IndexCount;
                        if (indexCount > 0)
                        {
                            renderPass.BindIndexBuffer(
                                command.Resources.IndexBuffer!,
                                SDL.GPUIndexElementSize.IndexElementSize32Bit);
                            renderPass.DrawIndexedPrimitives((uint)indexCount, instanceCount);
                        }
                        else
                        {
                            renderPass.DrawPrimitives(
                                (uint)command.Command.Mesh.VertexCount,
                                instanceCount);
                        }
                    }
                }
            }

            PresentFrame();
        }
        finally
        {
            _renderFrame?.Dispose();
            _renderFrame = null;
            _colorTarget = null;
            // Return any pooled CPU staging buffers (instance bytes) and
            // mark all acquired GPU instance buffers as free for the next
            // frame's commands.
            foreach (var c in _commands)
                c.Release();
            _commands.Clear();
            _instanceBuffersAcquired = 0;
            _debugTextVertexIndex = 0;
            _frameNumber++;
            SweepCaches();
        }
    }

    public virtual void Dispose()
    {
        Render();
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
                entry.Resources.IndexBuffer?.Dispose();
                entry.Resources.IndexUploadBuffer?.Dispose();
                entry.Resources.WireframeIndexBuffer?.Dispose();
                entry.Resources.WireframeIndexUploadBuffer?.Dispose();
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

    // Acquire a pooled instance-rate vertex buffer + matching upload
    // buffer of capacity >= bytes. The pool grows monotonically; buffers
    // marked acquired this frame are not reused again until frame end
    // resets _instanceBuffersAcquired.
    private InstanceBuffer AcquireInstanceBuffer(int bytes)
    {
        // Try to reuse a slot whose buffer is already big enough.
        for (int i = _instanceBuffersAcquired; i < _instanceBufferPool.Count; i++)
        {
            var candidate = _instanceBufferPool[i];
            if (candidate.CapacityBytes >= bytes)
            {
                if (i != _instanceBuffersAcquired)
                {
                    // Move the chosen entry to the front of the unused
                    // region so subsequent acquires don't keep scanning
                    // past it.
                    _instanceBufferPool[i] = _instanceBufferPool[_instanceBuffersAcquired];
                    _instanceBufferPool[_instanceBuffersAcquired] = candidate;
                }
                _instanceBuffersAcquired++;
                return candidate;
            }
        }

        // No big-enough free entry: either grow an existing too-small
        // one (avoids unbounded slot growth) or add a fresh slot.
        InstanceBuffer slot;
        if (_instanceBuffersAcquired < _instanceBufferPool.Count)
        {
            slot = _instanceBufferPool[_instanceBuffersAcquired];
            slot.Buffer?.Dispose();
            slot.Upload?.Dispose();
        }
        else
        {
            slot = new InstanceBuffer();
            _instanceBufferPool.Add(slot);
        }

        slot.Buffer = _device.CreateVertexBuffer<byte>(
            (uint)bytes,
            new GpuVertexBufferLayout
            {
                Pitch = 0,
                Elements = ImmutableArray<GpuShaderVertexElement>.Empty,
                InputRate = SDL.GPUVertexInputRate.Instance,
            });
        slot.Upload = (GpuUploadBuffer)GpuUploadBuffer.Create(_device, (uint)bytes);
        slot.CapacityBytes = bytes;
        _instanceBuffersAcquired++;
        return slot;
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
        ShaderVertexLayout? instanceLayout,
        DepthMode depthMode,
        BlendMode blendMode,
        CullMode cullMode,
        Topology topology)
    {
        var key = new PipelineKey(vertexShader, fragmentShader, colorFormat, layout, instanceLayout, depthMode, blendMode, cullMode, topology);
        if (!_pipelines.TryGetValue(key, out var pipeline))
        {
            pipeline = CreatePipeline(vertexShader, fragmentShader, colorFormat, layout, instanceLayout, depthMode, blendMode, cullMode, topology);
            _pipelines[key] = pipeline;
        }

        return pipeline;
    }

    private static void ApplyViewport(GpuRenderPass renderPass, Rect rect, uint targetWidth, uint targetHeight)
    {
        // SDL_GPU expects viewport coordinates in pixels of the render
        // target with (0, 0) at the top-left -- same convention Rect
        // uses, so no Y flip is needed.
        renderPass.SetViewport(new SDL.GPUViewport
        {
            X = rect.X,
            Y = rect.Y,
            W = rect.Width,
            H = rect.Height,
            MinDepth = 0f,
            MaxDepth = 1f,
        });
    }

    private static void ApplyClip(GpuRenderPass renderPass, Rect rect, uint targetWidth, uint targetHeight)
    {
        // SDL.Rect uses signed-int pixel coordinates; clamp Rect to the
        // target so we don't pass a scissor that exceeds it (which SDL
        // treats as an error).
        var x = (int)Math.Max(0f, rect.X);
        var y = (int)Math.Max(0f, rect.Y);
        var w = (int)Math.Min(rect.Width, targetWidth - x);
        var h = (int)Math.Min(rect.Height, targetHeight - y);
        if (w < 0) w = 0;
        if (h < 0) h = 0;
        renderPass.SetScissor(new SDL.Rect { X = x, Y = y, W = w, H = h });
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

    // Translate the user-facing BlendMode to the GPU color/alpha
    // blend factors and op. Alpha channel uses (One, OneMinusSrcAlpha,
    // Add) for every translucent mode -- this is the standard rule
    // for accumulating coverage when compositing into a target whose
    // alpha may itself be sampled later.
    private static SDL.GPUColorTargetBlendState MapBlendMode(BlendMode blendMode) => blendMode switch
    {
        BlendMode.Opaque => new SDL.GPUColorTargetBlendState
        {
            EnableBlend = 0,
        },
        BlendMode.Alpha => new SDL.GPUColorTargetBlendState
        {
            EnableBlend = 1,
            SrcColorBlendfactor = SDL.GPUBlendFactor.SrcAlpha,
            DstColorBlendfactor = SDL.GPUBlendFactor.OneMinusSrcAlpha,
            ColorBlendOp = SDL.GPUBlendOp.Add,
            SrcAlphaBlendfactor = SDL.GPUBlendFactor.One,
            DstAlphaBlendfactor = SDL.GPUBlendFactor.OneMinusSrcAlpha,
            AlphaBlendOp = SDL.GPUBlendOp.Add,
        },
        BlendMode.Additive => new SDL.GPUColorTargetBlendState
        {
            EnableBlend = 1,
            SrcColorBlendfactor = SDL.GPUBlendFactor.SrcAlpha,
            DstColorBlendfactor = SDL.GPUBlendFactor.One,
            ColorBlendOp = SDL.GPUBlendOp.Add,
            SrcAlphaBlendfactor = SDL.GPUBlendFactor.One,
            DstAlphaBlendfactor = SDL.GPUBlendFactor.OneMinusSrcAlpha,
            AlphaBlendOp = SDL.GPUBlendOp.Add,
        },
        BlendMode.Multiply => new SDL.GPUColorTargetBlendState
        {
            EnableBlend = 1,
            SrcColorBlendfactor = SDL.GPUBlendFactor.DstColor,
            DstColorBlendfactor = SDL.GPUBlendFactor.Zero,
            ColorBlendOp = SDL.GPUBlendOp.Add,
            SrcAlphaBlendfactor = SDL.GPUBlendFactor.One,
            DstAlphaBlendfactor = SDL.GPUBlendFactor.OneMinusSrcAlpha,
            AlphaBlendOp = SDL.GPUBlendOp.Add,
        },
        _ => throw new ArgumentOutOfRangeException(nameof(blendMode), blendMode, null),
    };

    private GpuPipeline CreatePipeline(
        GpuShader vertexShader,
        GpuShader fragmentShader,
        SDL.GPUTextureFormat colorFormat,
        ShaderVertexLayout layout,
        ShaderVertexLayout? instanceLayout,
        DepthMode depthMode,
        BlendMode blendMode,
        CullMode cullMode,
        Topology topology)
    {
        var attributes = ImmutableArray.CreateBuilder<SDL.GPUVertexAttribute>();
        uint location = 0;

        // Slot 0: per-vertex attributes from the mesh's vertex layout.
        uint vertexPitch = AppendVertexAttributes(attributes, layout, bufferSlot: 0, ref location);

        // Slot 1: per-instance attributes from the shader's instance
        // layout, when present. Locations continue past the per-vertex
        // ones, and a Matrix4x4 element expands to four consecutive
        // float4 attribute locations -- one per matrix row -- because
        // SDL/Vulkan/HLSL describe a mat4 vertex input that way.
        uint instancePitch = 0;
        if (instanceLayout is not null)
            instancePitch = AppendVertexAttributes(attributes, instanceLayout, bufferSlot: 1, ref location);

        var bufferDescriptionsBuilder = ImmutableArray.CreateBuilder<SDL.GPUVertexBufferDescription>(instanceLayout is null ? 1 : 2);
        bufferDescriptionsBuilder.Add(new SDL.GPUVertexBufferDescription
        {
            Slot = 0,
            Pitch = vertexPitch,
            InputRate = SDL.GPUVertexInputRate.Vertex,
            InstanceStepRate = 0,
        });
        if (instanceLayout is not null)
        {
            bufferDescriptionsBuilder.Add(new SDL.GPUVertexBufferDescription
            {
                Slot = 1,
                Pitch = instancePitch,
                InputRate = SDL.GPUVertexInputRate.Instance,
                InstanceStepRate = 1,
            });
        }

        var bufferDescriptions = bufferDescriptionsBuilder.ToImmutable();

        var colorTargets = ImmutableArray.Create(new SDL.GPUColorTargetDescription
        {
            Format = colorFormat,
            BlendState = MapBlendMode(blendMode),
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
            PrimitiveType = topology switch
            {
                Topology.TriangleList  => SDL.GPUPrimitiveType.TriangleList,
                Topology.TriangleStrip => SDL.GPUPrimitiveType.TriangleStrip,
                Topology.LineList      => SDL.GPUPrimitiveType.LineList,
                Topology.LineStrip     => SDL.GPUPrimitiveType.LineStrip,
                Topology.PointList     => SDL.GPUPrimitiveType.PointList,
                _ => throw new ArgumentOutOfRangeException(nameof(topology), topology, null),
            },
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
        ShaderVertexElementKind.Float4 => (SDL.GPUVertexElementFormat.Float4, 16u),
        // Matrix4x4 is not single-attribute on the GPU side -- it's handled
        // by AppendVertexAttributes which expands it to four Float4 rows.
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    // Appends the GPU vertex attributes for one ShaderVertexLayout to the
    // attributes builder, advancing the running attribute-location counter
    // and returning the per-vertex (or per-instance) byte stride. Matrix4x4
    // expands into four consecutive Float4 attribute locations -- one per
    // row -- because GPU vertex inputs are at most 16 bytes (vec4) wide.
    private static uint AppendVertexAttributes(
        ImmutableArray<SDL.GPUVertexAttribute>.Builder attributes,
        ShaderVertexLayout layout,
        uint bufferSlot,
        ref uint location)
    {
        uint offset = 0;
        foreach (var element in layout.Elements)
        {
            if (element.Kind == ShaderVertexElementKind.Matrix4x4)
            {
                for (int row = 0; row < 4; row++)
                {
                    attributes.Add(new SDL.GPUVertexAttribute
                    {
                        Location = location++,
                        BufferSlot = bufferSlot,
                        Format = SDL.GPUVertexElementFormat.Float4,
                        Offset = offset,
                    });
                    offset += 16;
                }
            }
            else
            {
                var (format, size) = MapShaderVertexElement(element.Kind);
                attributes.Add(new SDL.GPUVertexAttribute
                {
                    Location = location++,
                    BufferSlot = bufferSlot,
                    Format = format,
                    Offset = offset,
                });
                offset += size;
            }
        }
        return offset;
    }

    // Walks the per-instance byte buffer and transposes every Matrix4x4
    // field in place. See QueueInstanced for why this is needed.
    private static void TransposeInstanceMatrices(Span<byte> bytes, ShaderVertexLayout layout, int instanceCount)
    {
        // Collect matrix offsets within one instance. Most layouts have
        // exactly one matrix, so a tiny stack-allocated list is plenty.
        Span<int> matrixOffsets = stackalloc int[layout.Elements.Length];
        int matrixCount = 0;
        int instanceStride = 0;
        foreach (var element in layout.Elements)
        {
            if (element.Kind == ShaderVertexElementKind.Matrix4x4)
            {
                matrixOffsets[matrixCount++] = instanceStride;
                instanceStride += 64;
            }
            else
            {
                instanceStride += element.Kind switch
                {
                    ShaderVertexElementKind.Position3          => 12,
                    ShaderVertexElementKind.TextureCoordinate2 => 8,
                    ShaderVertexElementKind.Color4             => 4,
                    ShaderVertexElementKind.Float4             => 16,
                    _ => throw new ArgumentOutOfRangeException(nameof(layout)),
                };
            }
        }

        if (matrixCount == 0)
            return;

        for (int i = 0; i < instanceCount; i++)
        {
            int instanceBase = i * instanceStride;
            for (int m = 0; m < matrixCount; m++)
            {
                ref var matrix = ref MemoryMarshal.AsRef<Matrix4x4>(
                    bytes.Slice(instanceBase + matrixOffsets[m], 64));
                matrix = Matrix4x4.Transpose(matrix);
            }
        }
    }

    /// <summary>
    /// Builds a deduped LineList index buffer enumerating every unique
    /// edge of the mesh's triangles. Works for both <see cref="Topology.TriangleList"/>
    /// (step 3) and <see cref="Topology.TriangleStrip"/> (step 1) by
    /// adjusting the stride between successive triangles. Indices may
    /// be empty: in that case the triangle corners are taken from the
    /// vertex buffer in order. Edges are sorted as (min, max) before
    /// deduping so that triangle winding -- which alternates in strips
    /// anyway -- is irrelevant.
    /// </summary>
    private static uint[] BuildWireframeIndices(
        ReadOnlySpan<uint> sourceIndices,
        int vertexCount,
        Topology meshTopology)
    {
        int step = meshTopology switch
        {
            Topology.TriangleList  => 3,
            Topology.TriangleStrip => 1,
            _ => 0,
        };

        if (step == 0)
            return Array.Empty<uint>();

        var edges = new HashSet<(uint, uint)>();
        bool indexed = sourceIndices.Length > 0;
        int triCornerCount = indexed ? sourceIndices.Length : vertexCount;

        for (int i = 0; i + 2 < triCornerCount; i += step)
        {
            uint a = indexed ? sourceIndices[i]     : (uint)i;
            uint b = indexed ? sourceIndices[i + 1] : (uint)(i + 1);
            uint c = indexed ? sourceIndices[i + 2] : (uint)(i + 2);

            // Skip degenerate triangles (any two corners equal).
            // Strips often emit these intentionally as "restarts."
            if (a == b || b == c || a == c) continue;

            AddEdge(edges, a, b);
            AddEdge(edges, b, c);
            AddEdge(edges, a, c);
        }

        var result = new uint[edges.Count * 2];
        int j = 0;
        foreach (var (lo, hi) in edges)
        {
            result[j++] = lo;
            result[j++] = hi;
        }
        return result;

        static void AddEdge(HashSet<(uint, uint)> set, uint x, uint y) =>
            set.Add(x < y ? (x, y) : (y, x));
    }

    private readonly record struct PipelineKey(
        GpuShader VertexShader,
        GpuShader FragmentShader,
        SDL.GPUTextureFormat ColorFormat,
        ShaderVertexLayout Layout,
        ShaderVertexLayout? InstanceLayout,
        DepthMode DepthMode,
        BlendMode BlendMode,
        CullMode CullMode,
        Topology Topology);

    private record DrawCommand(
        Mesh Mesh,
        ShaderSet Shader,
        Image? Texture,
        GpuSampler? Sampler,
        DepthMode DepthMode,
        BlendMode BlendMode,
        CullMode CullMode,
        Topology Topology,
        bool Wireframe,
        Rect? Viewport,
        Rect? ClipRect)
    {
        /// <summary>
        /// Pushes any per-draw arguments this command carries to the given
        /// render pass. The base command has no args; the generic
        /// <see cref="DrawCommand{TArgs}"/> subtype overrides this to push
        /// the slots described by its <see cref="ShaderArgsLayout"/>.
        /// </summary>
        public virtual void PushArgs(GpuRenderPass renderPass) { }

        /// <summary>
        /// Per-instance vertex layout (slot 1) when this is an instanced
        /// draw, otherwise <see langword="null"/>. Drives the pipeline
        /// cache key and the second vertex-buffer bind in the render
        /// loop.
        /// </summary>
        public virtual ShaderVertexLayout? InstanceLayout => null;

        /// <summary>
        /// Number of instances to draw. Always 1 for non-instanced
        /// commands; the per-instance count for instanced ones.
        /// </summary>
        public virtual int InstanceCount => 1;

        /// <summary>
        /// Raw bytes of the per-instance buffer for instanced commands.
        /// Empty for non-instanced. The renderer copies these into a
        /// pooled GPU instance-rate vertex buffer for the frame.
        /// </summary>
        public virtual ReadOnlySpan<byte> InstanceBytes => ReadOnlySpan<byte>.Empty;

        /// <summary>
        /// Returns any pooled CPU buffers held by this command. Called
        /// at frame end after the GPU work has been recorded.
        /// </summary>
        public virtual void Release() { }
    }

    private sealed record DrawCommand<TArgs>(
        Mesh Mesh,
        ShaderSet Shader,
        Image? Texture,
        GpuSampler? Sampler,
        DepthMode DepthMode,
        BlendMode BlendMode,
        CullMode CullMode,
        Topology Topology,
        bool Wireframe,
        Rect? Viewport,
        Rect? ClipRect,
        ShaderArgsLayout Layout,
        TArgs Args)
        : DrawCommand(Mesh, Shader, Texture, Sampler, DepthMode, BlendMode, CullMode, Topology, Wireframe, Viewport, ClipRect)
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

    // Instanced draw. Owns a pooled byte[] holding the user's instance
    // span, copied at queue time so the user's storage doesn't have to
    // outlive the call. The render loop uploads InstanceBytes into a
    // pooled GPU instance-rate vertex buffer and binds it on slot 1.
    private sealed record InstancedDrawCommand<TArgs>(
        Mesh Mesh,
        ShaderSet Shader,
        Image? Texture,
        GpuSampler? Sampler,
        DepthMode DepthMode,
        BlendMode BlendMode,
        CullMode CullMode,
        Topology Topology,
        bool Wireframe,
        Rect? Viewport,
        Rect? ClipRect,
        ShaderArgsLayout ArgsLayoutValue,
        TArgs Args,
        ShaderVertexLayout InstanceLayoutValue,
        byte[] InstanceBytesArray,
        int InstanceBytesLength,
        int InstanceCountValue)
        : DrawCommand(Mesh, Shader, Texture, Sampler, DepthMode, BlendMode, CullMode, Topology, Wireframe, Viewport, ClipRect)
        where TArgs : unmanaged
    {
        public override ShaderVertexLayout? InstanceLayout => InstanceLayoutValue;
        public override int InstanceCount => InstanceCountValue;
        public override ReadOnlySpan<byte> InstanceBytes =>
            InstanceBytesArray.AsSpan(0, InstanceBytesLength);

        public override void PushArgs(GpuRenderPass renderPass)
        {
            var value = Args;
            var bytes = MemoryMarshal.AsBytes(
                MemoryMarshal.CreateReadOnlySpan(ref value, 1));
            int offset = 0;
            foreach (var element in ArgsLayoutValue.Elements)
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

        public override void Release()
        {
            ArrayPool<byte>.Shared.Return(InstanceBytesArray);
        }
    }

    // Pooled GPU instance buffer. Lives as long as the renderer; SDL
    // upload uses cycle:true so re-writing the same buffer in a later
    // frame is safe even if the previous frame is still in flight.
    private sealed class InstanceBuffer : IDisposable
    {
        public GpuVertexBuffer? Buffer;
        public GpuUploadBuffer? Upload;
        public int CapacityBytes;

        public void Dispose()
        {
            Buffer?.Dispose();
            Upload?.Dispose();
            Buffer = null;
            Upload = null;
            CapacityBytes = 0;
        }
    }

    private sealed class PreparedDrawCommand
    {
        public PreparedDrawCommand(DrawCommand command, MeshResources resources)
        {
            Command = command;
            Resources = resources;
        }

        public DrawCommand Command { get; }
        public MeshResources Resources { get; }

        // Set in the copy pass for instanced commands; null otherwise.
        // Holds the pooled GPU instance-rate vertex buffer the upload
        // was staged into and that the render pass binds on slot 1.
        public InstanceBuffer? Instance { get; set; }
    }

    private sealed class MeshResources
    {
        public GpuVertexBuffer? VertexBuffer { get; set; }
        public GpuUploadBuffer? UploadBuffer { get; set; }
        public int VertexBufferBytes { get; set; }
        public int VertexBufferCapacityBytes { get; set; }

        // Index buffer + its own staging buffer. Lazily created -- a mesh
        // with no indices keeps these null and uses the unindexed draw
        // path. Sized independently of the vertex buffer.
        public GpuIndexBuffer? IndexBuffer { get; set; }
        public GpuUploadBuffer? IndexUploadBuffer { get; set; }
        public int IndexBufferBytes { get; set; }
        public int IndexBufferCapacityBytes { get; set; }

        // Wireframe edge-index buffer + staging buffer, derived from the
        // mesh's triangle indices (or vertex order, when unindexed). Only
        // allocated when the mesh has actually been drawn at least once
        // with Renderer3D.Wireframe = true; meshes never drawn in
        // wireframe pay no cost.
        public GpuIndexBuffer? WireframeIndexBuffer { get; set; }
        public GpuUploadBuffer? WireframeIndexUploadBuffer { get; set; }
        public int WireframeIndexBufferBytes { get; set; }
        public int WireframeIndexBufferCapacityBytes { get; set; }
        public int WireframeIndexCount { get; set; }
        public int LastWireframeUploadedVersion { get; set; }

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
