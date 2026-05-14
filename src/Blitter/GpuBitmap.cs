namespace Blitter;

/// <summary>
/// A GPU-resident image.
/// </summary>
public sealed class GpuBitmap : Image
{
    private readonly GpuDevice _device;
    private GpuTexture? _texture;
    private int _version;

    internal GpuBitmap(GpuDevice device, int width, int height, PixelFormat format, int levels, bool renderTarget)
    {
        ArgumentNullException.ThrowIfNull(device);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(levels);

        _device = device;
        Width = width;
        Height = height;
        PixelFormat = format;
        LevelCount = levels;
        Mipmaps = levels > 1;

        var usage = SDL.GPUTextureUsageFlags.Sampler;
        if (renderTarget)
            usage |= SDL.GPUTextureUsageFlags.ColorTarget;

        _texture = GpuTexture.Create(device, new GpuTextureCreateInfo
        {
            Type = SDL.GPUTextureType.Texturetype2D,
            Format = GpuPixelFormatMap.ToGpu(format),
            Usage = usage,
            Width = (uint)width,
            Height = (uint)height,
            LayerCountOrDepth = 1,
            NumLevels = (uint)levels,
            SampleCount = SDL.GPUSampleCount.SampleCount1,
        });
        _version = 1;
    }

    /// <summary>
    /// Allocates a GPU-resident image of the given dimensions using
    /// the process's default GPU device.
    /// </summary>
    /// <param name="width">Width in pixels.</param>
    /// <param name="height">Height in pixels.</param>
    /// <param name="format">Pixel format. Must be a format with a GPU equivalent.</param>
    /// <param name="levels">Mip-level count; <c>1</c> for no mipmaps.</param>
    /// <param name="renderTarget">When <c>true</c>, the texture is usable as a color attachment.</param>
    public static GpuBitmap Create(int width, int height, PixelFormat format = PixelFormat.ABGR8888, int levels = 1, bool renderTarget = true)
        => new GpuBitmap(GpuDevice.Default, width, height, format, levels, renderTarget);

    /// <inheritdoc/>
    public override int Width { get; }

    /// <inheritdoc/>
    public override int Height { get; }

    /// <inheritdoc/>
    public override PixelFormat PixelFormat { get; }

    /// <inheritdoc/>
    public override int Version => _version;

    /// <inheritdoc/>
    public override int LevelCount { get; }

    /// <inheritdoc/>
    public override bool Mipmaps { get; }

    /// <inheritdoc/>
    public override bool IsDisposed => _texture is null || _texture.IsDisposed;

    /// <inheritdoc/>
    public override void Invalidate()
    {
        unchecked { _version++; }
    }

    internal GpuTexture Texture =>
        _texture ?? throw new ObjectDisposedException(nameof(GpuBitmap));

    /// <inheritdoc/>
    public override void Dispose()
    {
        var tex = Interlocked.Exchange(ref _texture, null);
        tex?.Dispose();
    }
}
