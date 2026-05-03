using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Numerics;

namespace Grape;
/// A high-level renderer for drawing a scene using the GPU pipeline.
/// </summary>
public sealed class WindowRenderer3D : Renderer3D, IDisposable
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
    private readonly List<DrawCommand> _commands = new();
    // Maps caller-owned vertex arrays to the renderer-owned Mesh that wraps
    // them. Lifetime is tied to the array reference: when the user drops
    // their array, the mesh becomes collectible, the renderer's weak ref
    // goes empty, and SweepCaches() disposes the GPU buffer.
    private readonly ConditionalWeakTable<Array, Mesh> _arrayMeshCache = new();
    private BuiltInShaders? _shaders;
    private GpuSampler? _defaultSampler;
    private GpuSampler? _debugTextSampler;
    private Image? _debugFontAtlas;
    // One vertex buffer per RenderDebugText call within a frame. Each draw
    // gets its own array so the array-keyed mesh cache resolves to a
    // distinct Mesh per draw; otherwise multiple text strings in the same
    // frame would all reference the same backing buffer and render as
    // whichever string was queued last.
    private readonly List<TextureVertex3D[]> _debugTextVertexBuffers = new();
    private int _debugTextVertexIndex;
    private long _frameNumber;

    // Per-frame state. Non-null between BeginFrame and Present.
    private GpuRenderFrame? _renderFrame;
    private GpuTexture? _colorTarget;
    private SDL.GPUTextureFormat _colorFormat;
    private SDL.FColor _clearColor;

    internal WindowRenderer3D(GpuDevice device)
    {
        _device = device;
    }

    /// <summary>
    /// The <see cref="GpuDevice"/> this renderer draws through.
    /// </summary>
    internal GpuDevice Device => _device;

    /// <summary>
    /// Lazy access to the precompiled shaders bundled with Grape.
    /// </summary>
    public override BuiltInShaders Shaders => _shaders ??= new BuiltInShaders(_device);

    /// <summary>
    /// A default linear-filtered, repeating sampler used by
    /// <see cref="RenderTexturedMesh"/> when the caller does not supply one.
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
                out _,
                out _) && swapchainTextureId != 0)
        {
            _colorTarget = GpuTexture.WrapBorrowed(swapchainTextureId);
        }
        else
        {
            _colorTarget = null;
        }
    }

    /// <summary>
    /// Draws a mesh using the given shader.
    /// </summary>
    /// <remarks>
    /// The vertex type of the mesh and the shader must match. The mesh's
    /// vertex layout must also match the shader's expected vertex layout.
    /// </remarks>
    public override void RenderMesh<TVertex>(Mesh<TVertex> mesh, Shader<TVertex> shader, Matrix4x4? transform = null)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        ArgumentNullException.ThrowIfNull(shader);

        if (mesh.Layout != shader.VertexLayout)
            throw new ArgumentException(
                "The mesh's vertex layout does not match the shader's expected vertex layout.",
                nameof(mesh));

        _commands.Add(new DrawCommand(mesh, shader, transform, Texture: null, Sampler: null));
    }

    /// <summary>
    /// Draws a mesh using the given shader, sourcing vertex data directly
    /// from a caller-owned array. The renderer keeps a weak association
    /// between the array reference and an internal <see cref="Mesh{TVertex}"/>;
    /// passing the same array on later frames reuses the cached GPU buffer
    /// and only re-uploads when the array's contents have changed.
    /// </summary>
    /// <remarks>
    /// To "resize" the mesh, allocate a new array — that's a different
    /// reference and gets a fresh GPU buffer. The previous array's GPU
    /// resources are released once you stop using it (either when the array
    /// is collected, or after a short idle period).
    /// </remarks>
    public override void RenderMesh<TVertex>(TVertex[] vertices, Shader<TVertex> shader, Matrix4x4? transform = null, int? vertexCount = null)
    {
        ArgumentNullException.ThrowIfNull(vertices);
        ArgumentNullException.ThrowIfNull(shader);

        var count = vertexCount ?? vertices.Length;
        if ((uint)count > (uint)vertices.Length)
            throw new ArgumentOutOfRangeException(nameof(vertexCount));

        var mesh = GetOrCreateArrayMesh(vertices, count, shader.VertexLayout);
        RenderMesh(mesh, shader, transform);
    }

    /// <summary>
    /// Draws a mesh using the given shader, sourcing vertex data from an
    /// <see cref="ImmutableArray{T}"/>. The renderer borrows the array's
    /// backing storage zero-copy and keeps a weak association keyed on it;
    /// passing the same <see cref="ImmutableArray{T}"/> (or any other built
    /// over the same underlying array) on later frames reuses the cached
    /// GPU buffer with no re-upload, since immutable arrays cannot change.
    /// </summary>
    public override void RenderMesh<TVertex>(ImmutableArray<TVertex> vertices, Shader<TVertex> shader, Matrix4x4? transform = null)
    {
        ArgumentNullException.ThrowIfNull(shader);
        if (vertices.IsDefault)
            throw new ArgumentException("ImmutableArray must be initialised.", nameof(vertices));

        var mesh = GetOrCreateImmutableArrayMesh(vertices, shader.VertexLayout);
        RenderMesh(mesh, shader, transform);
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
    public override void RenderTexturedMesh(
        Mesh<TextureVertex3D> mesh,
        Shader<TextureVertex3D> shader,
        Image texture,
        Matrix4x4? transform = null)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        ArgumentNullException.ThrowIfNull(shader);
        ArgumentNullException.ThrowIfNull(texture);

        if (mesh.Layout != shader.VertexLayout)
            throw new ArgumentException(
                "The mesh's vertex layout does not match the shader's expected vertex layout.",
                nameof(mesh));

        _commands.Add(new DrawCommand(mesh, shader, transform, texture, Sampler: null));
    }

    /// <summary>
    /// Draws a textured mesh using the given shader and image as the source
    /// texture, sourcing vertex data directly from a caller-owned array. See
    /// <see cref="RenderMesh{TVertex}(TVertex[], Shader{TVertex}, Matrix4x4?)"/>
    /// for caching semantics.
    /// </summary>
    public override void RenderTexturedMesh(
        TextureVertex3D[] vertices,
        Shader<TextureVertex3D> shader,
        Image texture,
        Matrix4x4? transform = null,
        int? vertexCount = null)
    {
        RenderTexturedMeshCore(vertices, shader, texture, sampler: null, transform, vertexCount);
    }

    internal void RenderTexturedMeshCore(
        TextureVertex3D[] vertices,
        Shader<TextureVertex3D> shader,
        Image texture,
        GpuSampler? sampler,
        Matrix4x4? transform,
        int? vertexCount)
    {
        ArgumentNullException.ThrowIfNull(vertices);
        ArgumentNullException.ThrowIfNull(shader);
        ArgumentNullException.ThrowIfNull(texture);

        var count = vertexCount ?? vertices.Length;
        if ((uint)count > (uint)vertices.Length)
            throw new ArgumentOutOfRangeException(nameof(vertexCount));

        var mesh = GetOrCreateArrayMesh(vertices, count, shader.VertexLayout);

        if (mesh.Layout != shader.VertexLayout)
            throw new ArgumentException(
                "The mesh's vertex layout does not match the shader's expected vertex layout.",
                nameof(vertices));

        _commands.Add(new DrawCommand(mesh, shader, transform, texture, sampler));
    }

    /// <summary>
    /// Draws a textured mesh using the given shader and image, sourcing
    /// vertex data from an <see cref="ImmutableArray{T}"/>. The renderer
    /// borrows the array's backing storage zero-copy. See
    /// <see cref="RenderMesh{TVertex}(ImmutableArray{TVertex}, Shader{TVertex}, Matrix4x4?)"/>
    /// for caching semantics.
    /// </summary>
    public override void RenderTexturedMesh(
        ImmutableArray<TextureVertex3D> vertices,
        Shader<TextureVertex3D> shader,
        Image texture,
        Matrix4x4? transform = null)
    {
        ArgumentNullException.ThrowIfNull(shader);
        ArgumentNullException.ThrowIfNull(texture);
        if (vertices.IsDefault)
            throw new ArgumentException("ImmutableArray must be initialised.", nameof(vertices));

        var mesh = GetOrCreateImmutableArrayMesh(vertices, shader.VertexLayout);
        RenderTexturedMesh(mesh, shader, texture, transform);
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
    public override void RenderDebugText(string text, Matrix4x4 transform)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.Length == 0)
            return;

        var atlas = GetDebugFontAtlas();

        // Each call within a frame needs its own backing array so the
        // array-keyed mesh cache produces a distinct Mesh per draw. We
        // pool the arrays across frames so we still get one GPU vertex
        // buffer per call site (assuming a stable per-frame call order).
        int needed = text.Length * 6;
        TextureVertex3D[] verts;
        if (_debugTextVertexIndex < _debugTextVertexBuffers.Count)
        {
            verts = _debugTextVertexBuffers[_debugTextVertexIndex];
            if (verts.Length < needed)
            {
                // Grow this slot in place; the new array becomes a different
                // cache key, so the previous mesh's GPU buffer will idle-evict.
                verts = new TextureVertex3D[needed];
                _debugTextVertexBuffers[_debugTextVertexIndex] = verts;
            }
        }
        else
        {
            verts = new TextureVertex3D[needed];
            _debugTextVertexBuffers.Add(verts);
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

        RenderTexturedMeshCore(
            verts,
            Shaders.TexturedQuadWithMatrix,
            atlas,
            sampler,
            transform,
            vertexCount: needed);
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

    private Mesh<TVertex> GetOrCreateArrayMesh<TVertex>(TVertex[] vertices, int count, VertexLayout layout)
        where TVertex : unmanaged
    {
        var span = vertices.AsSpan(0, count);

        if (_arrayMeshCache.TryGetValue(vertices, out var existing))
        {
            if (existing is not Mesh<TVertex> typed)
                throw new ArgumentException(
                    $"Vertex array was previously used with vertex type " +
                    $"'{existing!.GetType().GetGenericArguments().FirstOrDefault()?.Name ?? existing.GetType().Name}' " +
                    $"and cannot be reused with '{typeof(TVertex).Name}'.",
                    nameof(vertices));

            // Re-stage the latest contents. Reset bumps the mesh's Version,
            // and the upload loop only re-uploads when Version has changed.
            typed.Reset(span, ReadOnlySpan<uint>.Empty);
            return typed;
        }

        var mesh = new Mesh<TVertex>(span, ReadOnlySpan<uint>.Empty, layout);
        _arrayMeshCache.Add(vertices, mesh);
        return mesh;
    }

    private Mesh<TVertex> GetOrCreateImmutableArrayMesh<TVertex>(ImmutableArray<TVertex> vertices, VertexLayout layout)
        where TVertex : unmanaged
    {
        // Key on the immutable array's underlying T[]. Two ImmutableArrays
        // built over the same backing array hit the same cache entry, which
        // is fine since neither can mutate.
        var backing = ImmutableCollectionsMarshal.AsArray(vertices) ?? Array.Empty<TVertex>();

        if (_arrayMeshCache.TryGetValue(backing, out var existing))
        {
            if (existing is not Mesh<TVertex> typed)
                throw new ArgumentException(
                    $"Vertex array was previously used with vertex type " +
                    $"'{existing!.GetType().GetGenericArguments().FirstOrDefault()?.Name ?? existing.GetType().Name}' " +
                    $"and cannot be reused with '{typeof(TVertex).Name}'.",
                    nameof(vertices));

            // Immutable: contents can't have changed, so no Reset needed.
            return typed;
        }

        // Borrow the backing array zero-copy via the ImmutableArray ctor.
        var mesh = new Mesh<TVertex>(vertices, ImmutableArray<uint>.Empty, layout);
        _arrayMeshCache.Add(backing, mesh);
        return mesh;
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
                                    Elements = ImmutableArray<GpuVertexElement>.Empty
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

            if (!_renderFrame.TryBeginRenderPass(
                colorTargets,
                new GpuDepthStencilTargetInfo(),
                out var renderPass))
            {
                return;
            }

            using (renderPass)
            {
                foreach (var command in prepared)
                {
                    var shader = command.Command.Shader;
                    var pipeline = GetOrCreatePipeline(
                        shader.VertexShader,
                        shader.FragmentShader,
                        _colorFormat,
                        shader.VertexLayout);
                    renderPass!.BindGraphicsPipeline(pipeline);
                    renderPass.BindVertexBuffers([command.Resources.VertexBuffer!]);
                    if (shader.RequiresTransform)
                    {
                        // System.Numerics.Matrix4x4 is row-major in memory;
                        // HLSL reads cbuffer matrices column-major by default.
                        // The two interpretations cancel out: pushing the
                        // System.Numerics matrix raw produces the same
                        // transformation HLSL's mul(M, v) does for a
                        // column-vector v that csharp's `v * M` does for a
                        // row-vector v. So no transpose is needed.
                        var transform = command.Command.Transform ?? Matrix4x4.Identity;
                        renderPass.PushVertexUniformData(0, in transform);
                    }
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
        VertexLayout layout)
    {
        var key = new PipelineKey(vertexShader, fragmentShader, colorFormat, layout);
        if (!_pipelines.TryGetValue(key, out var pipeline))
        {
            pipeline = CreatePipeline(vertexShader, fragmentShader, colorFormat, layout);
            _pipelines[key] = pipeline;
        }

        return pipeline;
    }

    private GpuPipeline CreatePipeline(
        GpuShader vertexShader,
        GpuShader fragmentShader,
        SDL.GPUTextureFormat colorFormat,
        VertexLayout layout)
    {
        var attributes = ImmutableArray.CreateBuilder<SDL.GPUVertexAttribute>(layout.Elements.Length);
        uint offset = 0;
        for (var i = 0; i < layout.Elements.Length; i++)
        {
            var element = layout.Elements[i];
            var (format, size) = MapVertexElement(element.Kind);
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
            TargetInfo = new GpuPipelineTargetInfo
            {
                ColorTargetDescriptions = colorTargets,
            },
        });
    }

    private static (SDL.GPUVertexElementFormat Format, uint Size) MapVertexElement(VertexElementKind kind) => kind switch
    {
        VertexElementKind.Position3 => (SDL.GPUVertexElementFormat.Float3, 12u),
        VertexElementKind.TextureCoordinate2 => (SDL.GPUVertexElementFormat.Float2, 8u),
        VertexElementKind.Color4 => (SDL.GPUVertexElementFormat.Ubyte4Norm, 4u),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    private readonly record struct PipelineKey(
        GpuShader VertexShader,
        GpuShader FragmentShader,
        SDL.GPUTextureFormat ColorFormat,
        VertexLayout Layout);

    private sealed record DrawCommand(
        Mesh Mesh,
        Shader Shader,
        Matrix4x4? Transform,
        Image? Texture,
        GpuSampler? Sampler);

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
