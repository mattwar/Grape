using System.Collections.Immutable;
using System.Runtime.InteropServices;
using System.Numerics;

namespace SDL3.Model;

/// <summary>
/// A vertex+fragment shader pair bound to a specific vertex layout.
/// </summary>
public abstract class Shader
{
    private protected Shader(
        GpuShader vertexShader,
        GpuShader fragmentShader,
        GpuVertexLayout vertexLayout,
        bool requiresTransform)
    {
        ArgumentNullException.ThrowIfNull(vertexShader);
        ArgumentNullException.ThrowIfNull(fragmentShader);
        ArgumentNullException.ThrowIfNull(vertexLayout);

        VertexShader = vertexShader;
        FragmentShader = fragmentShader;
        VertexLayout = vertexLayout;
        RequiresTransform = requiresTransform;
    }

    public GpuShader VertexShader { get; }
    public GpuShader FragmentShader { get; }
    public GpuVertexLayout VertexLayout { get; }

    /// <summary>
    /// True if the vertex shader reads a 4x4 transformation matrix from
    /// vertex uniform slot 0. The renderer will push the per-draw transform
    /// (or <see cref="Matrix4x4.Identity"/>) before each draw call.
    /// </summary>
    public bool RequiresTransform { get; }
}

/// <summary>
/// A vertex+fragment shader pair bound to a specific vertex layout.
/// </summary>
/// <typeparam name="TVertex">
/// The vertex struct that meshes drawn with this shader must use.
/// </typeparam>
public sealed class Shader<TVertex> : Shader where TVertex : unmanaged
{
    public Shader(
        GpuShader vertexShader,
        GpuShader fragmentShader,
        GpuVertexLayout vertexLayout,
        bool requiresTransform = false)
        : base(vertexShader, fragmentShader, vertexLayout, requiresTransform)
    {
    }
}

/// <summary>
/// A high-level renderer for drawing a scene using the GPU pipeline.
/// </summary>
public sealed class Renderer3D : IDisposable
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
    private BuiltInShaders? _shaders;
    private GpuSampler? _defaultSampler;
    private long _frameNumber;

    // Per-frame state. Non-null between BeginFrame and Present.
    private GpuRenderFrame? _renderFrame;
    private GpuTexture? _colorTarget;
    private SDL.GPUTextureFormat _colorFormat;
    private SDL.FColor _clearColor;

    public Renderer3D(GpuDevice device)
    {
        _device = device;
    }

    /// <summary>
    /// The <see cref="GpuDevice"/> this renderer draws through.
    /// </summary>
    public GpuDevice Device => _device;

    /// <summary>
    /// Lazy access to the precompiled shaders bundled with SDL3.Model.
    /// </summary>
    public BuiltInShaders Shaders => _shaders ??= new BuiltInShaders(_device);

    /// <summary>
    /// A default linear-filtered, repeating sampler used by
    /// <see cref="RenderTexturedMesh"/> when the caller does not supply one.
    /// </summary>
    public GpuSampler DefaultSampler => _defaultSampler ??= _device.CreateSampler(new GpuSamplerCreateInfo
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
    public void RenderMesh<TVertex>(Mesh<TVertex> mesh, Shader<TVertex> shader, Matrix4x4? transform = null)
        where TVertex : unmanaged
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
    /// Draws a textured mesh using the given shader and surface as the source texture.
    /// </summary>
    /// <remarks>
    /// The mesh and shader must both use <see cref="TextureVertex3D"/> and
    /// the mesh's vertex layout must match the shader's expected vertex layout.
    /// The surface's pixels are uploaded once to a GPU texture and cached;
    /// passing the same <see cref="Surface"/> instance on later frames reuses
    /// the upload.
    /// </remarks>
    public void RenderTexturedMesh(
        Mesh<TextureVertex3D> mesh,
        Shader<TextureVertex3D> shader,
        Surface texture,
        GpuSampler? sampler = null,
        Matrix4x4? transform = null)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        ArgumentNullException.ThrowIfNull(shader);
        ArgumentNullException.ThrowIfNull(texture);

        if (mesh.Layout != shader.VertexLayout)
            throw new ArgumentException(
                "The mesh's vertex layout does not match the shader's expected vertex layout.",
                nameof(mesh));

        _commands.Add(new DrawCommand(mesh, shader, transform, texture, sampler));
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
                    var vertexBytes = command.Command.Mesh.GetVertexBytes();
                    var resources = command.Resources;

                    if (resources.VertexBufferBytes != vertexBytes.Length)
                    {
                        resources.VertexBuffer?.Dispose();
                        resources.UploadBuffer?.Dispose();

                        resources.VertexBuffer = _device.CreateVertexBuffer<byte>(
                            (uint)vertexBytes.Length,
                            new GpuVertexBufferLayout
                            {
                                Pitch = vertexBytes.Length / command.Command.Mesh.VertexCount,
                                Elements = ImmutableArray<GpuVertexElement>.Empty
                            });

                        resources.UploadBuffer = (GpuUploadBuffer)GpuUploadBuffer.Create(_device, (uint)vertexBytes.Length);
                        resources.VertexBufferBytes = vertexBytes.Length;
                    }

                    copyPass!.Upload(resources.UploadBuffer!, resources.VertexBuffer!, vertexBytes);

                    if (command.Command.Texture is { } surface)
                        EnsureTextureUploaded(copyPass, surface);
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
                    if (command.Command.Texture is { } surface)
                    {
                        var gpuTexture = LookupTexture(surface)
                            ?? throw new InvalidOperationException(
                                "Texture upload was not recorded for this surface.");
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

    private GpuTexture? LookupTexture(Surface surface)
    {
        for (int i = 0; i < _textureResources.Count; i++)
        {
            var entry = _textureResources[i];
            if (entry.Surface.TryGetTarget(out var target) && ReferenceEquals(target, surface))
            {
                entry.LastUsedFrame = _frameNumber;
                return entry.Texture;
            }
        }

        return null;
    }

    /// <summary>
    /// Drops cached entries whose source mesh/surface has been garbage
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
            var dead = !entry.Surface.TryGetTarget(out _);
            if (idle || dead)
            {
                entry.Texture.Dispose();
                _textureResources.RemoveAt(i);
            }
        }
    }

    private void EnsureTextureUploaded(GpuCopyPass copyPass, Surface surface)
    {
        for (int i = 0; i < _textureResources.Count; i++)
        {
            var entry = _textureResources[i];
            if (entry.Surface.TryGetTarget(out var target) && ReferenceEquals(target, surface))
            {
                entry.LastUsedFrame = _frameNumber;
                return;
            }
        }

        var (width, height) = surface.Size;
        var format = MapPixelFormat(surface.PixelFormat);

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

        var pixels = surface.GetPixels();
        using var upload = (GpuUploadBuffer)GpuUploadBuffer.Create(_device, (uint)pixels.Length);
        copyPass.UploadToTexture(upload, gpuTexture, (uint)width, (uint)height, pixels);

        _textureResources.Add(new TextureCacheEntry(
            new WeakReference<Surface>(surface), gpuTexture, _frameNumber));
    }

    private static SDL.GPUTextureFormat MapPixelFormat(SDL.PixelFormat format) => format switch
    {
        // SDL_PIXELFORMAT_ABGR8888 stores bytes in memory as R, G, B, A on
        // little-endian platforms, matching SDL_GPU R8G8B8A8_UNORM.
        SDL.PixelFormat.ABGR8888 => SDL.GPUTextureFormat.R8G8B8A8Unorm,
        SDL.PixelFormat.ARGB8888 => SDL.GPUTextureFormat.B8G8R8A8Unorm,
        _ => throw new NotSupportedException(
            $"Surface pixel format '{format}' has no GPU texture format mapping. " +
            "Convert the surface to ABGR8888 before sampling on the GPU."),
    };

    private GpuPipeline GetOrCreatePipeline(
        GpuShader vertexShader,
        GpuShader fragmentShader,
        SDL.GPUTextureFormat colorFormat,
        GpuVertexLayout layout)
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
        GpuVertexLayout layout)
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
        GpuVertexLayout Layout);

    private sealed record DrawCommand(
        Mesh Mesh,
        Shader Shader,
        Matrix4x4? Transform,
        Surface? Texture,
        GpuSampler? Sampler);

    private sealed record PreparedDrawCommand(
        DrawCommand Command,
        MeshResources Resources);

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
        public TextureCacheEntry(WeakReference<Surface> surface, GpuTexture texture, long lastUsedFrame)
        {
            Surface = surface;
            Texture = texture;
            LastUsedFrame = lastUsedFrame;
        }

        public WeakReference<Surface> Surface { get; }
        public GpuTexture Texture { get; }
        public long LastUsedFrame { get; set; }
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

/// <summary>
/// CPU-side mesh data used by the high-level renderer. Mesh instances are
/// compared by reference identity; reuse the same instance frame-to-frame to
/// reuse the cached GPU vertex buffer.
/// </summary>
public abstract class Mesh
{
    private protected Mesh() { }

    public abstract int VertexCount { get; }
    public abstract int IndexCount { get; }
    public abstract GpuVertexLayout Layout { get; }
    public abstract ReadOnlySpan<byte> GetVertexBytes();
    public abstract ReadOnlySpan<uint> GetIndices();

    /// <summary>
    /// Bumped each time the mesh's contents are replaced. The renderer uses
    /// this to detect when its cached GPU vertex buffer needs to be re-uploaded.
    /// </summary>
    public int Version { get; private protected set; }
}

/// <summary>
/// CPU-Side mesh data used by <see cref="Renderer3D"/>.
/// Can be updated with fresh vertex/index data.
/// </summary>
public class Mesh<TVertex> : Mesh
    where TVertex : unmanaged
{
    // Either an array we own (writable) or one borrowed from an
    // ImmutableArray (must not be mutated). _ownsVertices/_ownsIndices
    // tells which.
    private TVertex[] _vertices;
    private int _vertexCount;
    private bool _ownsVertices;

    private uint[] _indices;
    private int _indexCount;
    private bool _ownsIndices;

    public Mesh(
        ReadOnlySpan<TVertex> vertices,
        ReadOnlySpan<uint> indices,
        GpuVertexLayout vertexLayout)
    {
        ArgumentNullException.ThrowIfNull(vertexLayout);

        _vertices = vertices.Length == 0 ? Array.Empty<TVertex>() : new TVertex[vertices.Length];
        vertices.CopyTo(_vertices);
        _vertexCount = vertices.Length;
        _ownsVertices = true;

        _indices = indices.Length == 0 ? Array.Empty<uint>() : new uint[indices.Length];
        indices.CopyTo(_indices);
        _indexCount = indices.Length;
        _ownsIndices = true;

        VertexLayout = vertexLayout;
        Version = 1;
    }

    public Mesh(
        ImmutableArray<TVertex> vertices,
        ImmutableArray<uint> indices,
        GpuVertexLayout vertexLayout)
    {
        ArgumentNullException.ThrowIfNull(vertexLayout);
        ThrowIfDefault(vertices, nameof(vertices));
        ThrowIfDefault(indices, nameof(indices));

        // Borrow the immutable arrays' backing storage directly. Immutable
        // arrays guarantee their contents won't change, so we can safely
        // read from them without copying.
        _vertices = ImmutableCollectionsMarshal.AsArray(vertices) ?? Array.Empty<TVertex>();
        _vertexCount = vertices.Length;
        _ownsVertices = false;

        _indices = ImmutableCollectionsMarshal.AsArray(indices) ?? Array.Empty<uint>();
        _indexCount = indices.Length;
        _ownsIndices = false;

        VertexLayout = vertexLayout;
        Version = 1;
    }

    public GpuVertexLayout VertexLayout { get; }

    public override int VertexCount => _vertexCount;

    public override int IndexCount => _indexCount;

    public override GpuVertexLayout Layout => VertexLayout;

    public override ReadOnlySpan<byte> GetVertexBytes() =>
        MemoryMarshal.AsBytes(_vertices.AsSpan(0, _vertexCount));

    public override ReadOnlySpan<uint> GetIndices() =>
        _indices.AsSpan(0, _indexCount);

    /// <summary>
    /// Replaces the vertex and index data with new contents copied from the
    /// supplied spans. The mesh's vertex layout is unchanged. Bumps
    /// <see cref="Mesh.Version"/> so the renderer re-uploads the GPU
    /// buffer on the next draw.
    /// </summary>
    public void Reset(ReadOnlySpan<TVertex> vertices, ReadOnlySpan<uint> indices)
    {
        EnsureOwnedVertexCapacity(vertices.Length);
        vertices.CopyTo(_vertices);
        _vertexCount = vertices.Length;

        EnsureOwnedIndexCapacity(indices.Length);
        indices.CopyTo(_indices);
        _indexCount = indices.Length;

        unchecked { Version++; }
    }

    /// <summary>
    /// Replaces the vertex and index data with the supplied immutable arrays.
    /// The arrays' underlying storage is borrowed in-place (zero copy).
    /// Bumps <see cref="Mesh.Version"/>.
    /// </summary>
    public void Reset(ImmutableArray<TVertex> vertices, ImmutableArray<uint> indices)
    {
        ThrowIfDefault(vertices, nameof(vertices));
        ThrowIfDefault(indices, nameof(indices));

        _vertices = ImmutableCollectionsMarshal.AsArray(vertices) ?? Array.Empty<TVertex>();
        _vertexCount = vertices.Length;
        _ownsVertices = false;

        _indices = ImmutableCollectionsMarshal.AsArray(indices) ?? Array.Empty<uint>();
        _indexCount = indices.Length;
        _ownsIndices = false;

        unchecked { Version++; }
    }

    private void EnsureOwnedVertexCapacity(int count)
    {
        if (!_ownsVertices || _vertices.Length < count)
        {
            // Either we're holding a borrowed (immutable) array we mustn't
            // overwrite, or the owned array is too small. Allocate fresh.
            _vertices = count == 0 ? Array.Empty<TVertex>() : new TVertex[count];
            _ownsVertices = true;
        }
    }

    private void EnsureOwnedIndexCapacity(int count)
    {
        if (!_ownsIndices || _indices.Length < count)
        {
            _indices = count == 0 ? Array.Empty<uint>() : new uint[count];
            _ownsIndices = true;
        }
    }

    private static void ThrowIfDefault<T>(ImmutableArray<T> array, string paramName)
    {
        if (array.IsDefault)
            throw new ArgumentException("ImmutableArray must be initialised.", paramName);
    }
}

/// <summary>
/// Mesh with position-only vertices.
/// </summary>
public sealed class VertexOnlyMesh : Mesh<Vertex3D>
{
    public VertexOnlyMesh(ReadOnlySpan<Vertex3D> vertices, ReadOnlySpan<uint> indices)
        : base(vertices, indices, VertexLayout)
    {
    }

    public VertexOnlyMesh(ImmutableArray<Vertex3D> vertices, ImmutableArray<uint> indices)
        : base(vertices, indices, VertexLayout)
    {
    }

    /// <summary>
    /// The default vertex layout used by the built-in mesh type.
    /// </summary>
    public static new GpuVertexLayout VertexLayout { get; } = new(
        ImmutableArray.Create(
            new MeshVertexElement(VertexElementKind.Position3)),
        1);
}

/// <summary>
/// A vertex with with just a 3D position.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly struct Vertex3D
{
    public readonly float X;
    public readonly float Y;
    public readonly float Z;

    public Vertex3D(float x, float y, float z)
    {
        X = x;
        Y = y;
        Z = z;
    }
}

/// <summary>
/// A vertex with position and color
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly struct ColorVertex3D
{
    public readonly Vertex3D Position;
    public readonly SDL.Color Color;

    public ColorVertex3D(Vertex3D vertex, SDL.Color color)
    {
        Position = vertex;
        Color = color;
    }
}

/// <summary>
/// A vertex that carries a position and a texture coordinate. Matches the
/// vertex input of the bundled <c>TexturedQuad</c> shaders.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly struct TextureVertex3D
{
    public readonly Vertex3D Position;
    public readonly Vector2 TextureCoordinate;

    public TextureVertex3D(Vertex3D vertex, Vector2 textureCoordinate)
    {
        Position = vertex;
        TextureCoordinate = textureCoordinate;
    }
}

/// <summary>
/// Mesh with vertex positions and colors.
/// </summary>
public sealed class ColoredMesh : Mesh<ColorVertex3D>
{
    public ColoredMesh(ReadOnlySpan<ColorVertex3D> vertices, ReadOnlySpan<uint> indices)
        : base(vertices, indices, VertexLayout)
    {
    }

    public ColoredMesh(ImmutableArray<ColorVertex3D> vertices, ImmutableArray<uint> indices)
        : base(vertices, indices, VertexLayout)
    {
    }

    /// <summary>
    /// The default vertex layout used by the colored mesh type.
    /// </summary>
    public static new GpuVertexLayout VertexLayout { get; } = new(
        ImmutableArray.Create(
            new MeshVertexElement(VertexElementKind.Position3),
            new MeshVertexElement(VertexElementKind.Color4)),
        1);
}

/// <summary>
/// Mesh with vertex positions and texture coordinates.
/// </summary>
public sealed class TexturedMesh : Mesh<TextureVertex3D>
{
    public TexturedMesh(ReadOnlySpan<TextureVertex3D> vertices, ReadOnlySpan<uint> indices)
        : base(vertices, indices, VertexLayout)
    {
    }

    public TexturedMesh(ImmutableArray<TextureVertex3D> vertices, ImmutableArray<uint> indices)
        : base(vertices, indices, VertexLayout)
    {
    }

    /// <summary>
    /// The default vertex layout used by the textured mesh type.
    /// </summary>
    public static new GpuVertexLayout VertexLayout { get; } = new(
        ImmutableArray.Create(
            new MeshVertexElement(VertexElementKind.Position3),
            new MeshVertexElement(VertexElementKind.TextureCoordinate2)),
        1);
}

/// <summary>
/// Describes the layout of vertices in a mesh.
/// </summary>
public sealed record GpuVertexLayout(ImmutableArray<MeshVertexElement> Elements, int VertexBufferSlotCount = 1);

/// <summary>
/// Describes one element in a vertex layout.
/// </summary>
public sealed record MeshVertexElement(VertexElementKind Kind);

/// <summary>
/// The kind of data represented by a vertex element.
/// </summary>
public enum VertexElementKind
{
    Position3,
    TextureCoordinate2,
    Color4
}