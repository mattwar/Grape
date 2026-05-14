namespace Blitter;

/// <summary>
/// A <see cref="CubeTexture"/> whose six faces live on the GPU as
/// a single layered <c>SDL_GPUTexture</c> (<c>TexturetypeCube</c>).
/// Use when the cubemap's contents are produced by the GPU (e.g.
/// baked irradiance / prefiltered specular environment maps) and
/// never need to be read back to the CPU. Renderers bind it directly
/// with no upload cost.
/// </summary>
public sealed class GpuCubemap : CubeTexture, IDisposable
{
    private readonly GpuDevice _device;
    private GpuTexture? _texture;
    private int _version;

    internal GpuCubemap(GpuDevice device, int size, PixelFormat format, int levels, bool renderTarget)
    {
        ArgumentNullException.ThrowIfNull(device);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(levels);

        _device = device;
        Size = size;
        Format = format;
        LevelCount = levels;
        Mipmaps = levels > 1;

        var usage = SDL.GPUTextureUsageFlags.Sampler;
        if (renderTarget)
            usage |= SDL.GPUTextureUsageFlags.ColorTarget;

        _texture = GpuTexture.Create(device, new GpuTextureCreateInfo
        {
            Type = SDL.GPUTextureType.TexturetypeCube,
            Format = GpuPixelFormatMap.ToGpu(format),
            Usage = usage,
            Width = (uint)size,
            Height = (uint)size,
            LayerCountOrDepth = 6,
            NumLevels = (uint)levels,
            SampleCount = SDL.GPUSampleCount.SampleCount1,
        });
        _version = 1;
    }

    /// <summary>
    /// Allocates a GPU-resident cubemap of the given edge size using
    /// the process's default GPU device.
    /// </summary>
    /// <param name="size">Edge length per face, in pixels.</param>
    /// <param name="format">Pixel format. Must have a GPU equivalent.</param>
    /// <param name="levels">Mip-level count per face; <c>1</c> for no mipmaps.</param>
    /// <param name="renderTarget">When <c>true</c>, the texture is usable as a color attachment.</param>
    public static GpuCubemap Create(int size, PixelFormat format = PixelFormat.ABGR8888, int levels = 1, bool renderTarget = true)
        => new GpuCubemap(GpuDevice.Default, size, format, levels, renderTarget);

    /// <inheritdoc/>
    public override int Size { get; }

    /// <inheritdoc/>
    public override PixelFormat Format { get; }

    /// <inheritdoc/>
    public override int LevelCount { get; }

    /// <inheritdoc/>
    public override bool Mipmaps { get; }

    /// <inheritdoc/>
    public override int Version => _version;

    /// <inheritdoc/>
    public override void Invalidate()
    {
        unchecked { _version++; }
    }

    /// <summary>Whether the cubemap has been disposed.</summary>
    public bool IsDisposed => _texture is null || _texture.IsDisposed;

    internal GpuTexture Texture =>
        _texture ?? throw new ObjectDisposedException(nameof(GpuCubemap));

    /// <summary>
    /// Render into each of the six faces of this cubemap, preserving
    /// any existing pixels in each face. The callback runs once per
    /// face (base mip level) with a renderer already targeting that
    /// face. The user sets up their own camera per face if needed.
    /// </summary>
    /// <param name="drawAction">Callback invoked for each face.</param>
    public void Render(Action<Renderer3D, CubeFace> drawAction)
        => RenderCore(null, drawAction);

    /// <summary>
    /// Render into each of the six faces of this cubemap, clearing
    /// each face to <paramref name="backgroundColor"/> first.
    /// </summary>
    /// <param name="backgroundColor">The background painted behind the draws on every face.</param>
    /// <param name="drawAction">Callback invoked for each face.</param>
    public void Render(Color backgroundColor, Action<Renderer3D, CubeFace> drawAction)
        => RenderCore(backgroundColor, drawAction);

    private void RenderCore(Color? backgroundColor, Action<Renderer3D, CubeFace> drawAction)
    {
        ArgumentNullException.ThrowIfNull(drawAction);
        if (IsDisposed)
            throw new ObjectDisposedException(nameof(GpuCubemap));

        foreach (var face in CubeFaceExtensions.All)
        {
            using var renderer = new GpuCubemapFaceRenderer(_device, this, face, mip: 0);
            if (backgroundColor is { } c)
            {
                renderer.BackgroundColor = c;
                renderer.AutoClear = true;
            }
            else
            {
                renderer.AutoClear = false;
            }
            drawAction(renderer, face);
            renderer.Render();
        }
        // Lower mips (if any) become stale after a render into level 0;
        // bump the version so dependent caches refresh.
        Invalidate();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        var tex = Interlocked.Exchange(ref _texture, null);
        tex?.Dispose();
    }
}
