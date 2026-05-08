using static SDL3.SDL;

namespace Grape;

/// <summary>
/// A GPU sampler resource that controls how textures are sampled by shaders.
/// </summary>
internal sealed class GpuSampler : IDisposable
{
    private readonly GpuDevice _gpuDevice;
    private nint _samplerId;

    private GpuSampler(GpuDevice device, nint samplerId)
    {
        _gpuDevice = device;
        _samplerId = samplerId;
        device.AddResource(this);
    }

    internal static GpuSampler Create(GpuDevice device, GpuSamplerCreateInfo info)
    {
        var nativeInfo = new SDL.GPUSamplerCreateInfo
        {
            MinFilter = info.MinFilter,
            MagFilter = info.MagFilter,
            MipmapMode = info.MipmapMode,
            AddressModeU = info.AddressModeU,
            AddressModeV = info.AddressModeV,
            AddressModeW = info.AddressModeW,
            MipLodBias = info.MipLodBias,
            MaxAnisotropy = info.MaxAnisotropy,
            CompareOp = info.CompareOp,
            MinLod = info.MinLod,
            MaxLod = info.MaxLod,
            EnableAnisotropy = (byte)(info.EnableAnisotropy ? 1 : 0),
            EnableCompare = (byte)(info.EnableCompare ? 1 : 0),
        };

        var samplerId = SDL.CreateGPUSampler(device.GpuDeviceID, nativeInfo);
        return new GpuSampler(device, samplerId);
    }

    internal nint SamplerId => _samplerId;

    public bool IsDisposed => _samplerId == 0;

    public void Dispose()
    {
        var id = Interlocked.Exchange(ref _samplerId, 0);
        if (id != 0)
        {
            _gpuDevice.RemoveResource(this);
            SDL.ReleaseGPUSampler(_gpuDevice.GpuDeviceID, id);
        }
    }
}
