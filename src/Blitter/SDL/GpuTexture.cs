using static SDL3.SDL;

namespace Blitter;

/// <summary>
/// A GPU texture resource that can be used as render target or sampled data.
/// </summary>
internal sealed class GpuTexture : IDisposable
{
    private readonly GpuDevice? _gpuDevice;
    private nint _textureId;
    private readonly bool _owned;

    private GpuTexture(GpuDevice device, nint textureId)
    {
        _gpuDevice = device;
        _textureId = textureId;
        _owned = true;
        device.AddResource(this);
    }

    private GpuTexture(nint textureId)
    {
        _gpuDevice = null;
        _textureId = textureId;
        _owned = false;
    }

    /// <summary>
    /// Wraps a texture handle whose lifetime is owned by something else (e.g.
    /// a swapchain). Disposing this wrapper does not release the underlying
    /// texture.
    /// </summary>
    internal static GpuTexture WrapBorrowed(nint textureId) => new GpuTexture(textureId);

    internal static GpuTexture Create(GpuDevice device, GpuTextureCreateInfo info)
    {
        var nativeInfo = new SDL.GPUTextureCreateInfo
        {
            Type = info.Type,
            Format = info.Format,
            Usage = info.Usage,
            Width = info.Width,
            Height = info.Height,
            LayerCountOrDepth = info.LayerCountOrDepth,
            NumLevels = info.NumLevels,
            SampleCount = info.SampleCount,
            Props = info.Properties?.PropertiesId ?? 0
        };

        var textureId = SDL.CreateGPUTexture(device.GpuDeviceID, nativeInfo);
        return new GpuTexture(device, textureId);
    }

    internal nint TextureId => _textureId;

    public bool IsDisposed => _textureId == 0;

    public void Dispose()
    {
        var id = Interlocked.Exchange(ref _textureId, 0);
        if (id != 0 && _owned && _gpuDevice is not null)
        {
            _gpuDevice.RemoveResource(this);
            SDL.ReleaseGPUTexture(_gpuDevice.GpuDeviceID, id);
        }
    }
}
