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

    /// <inheritdoc/>
    public void Dispose()
    {
        var tex = Interlocked.Exchange(ref _texture, null);
        tex?.Dispose();
    }
}
