namespace Blitter;

/// <summary>
/// A GPU shader module used when creating pipelines.
/// </summary>
internal sealed class GpuShader : IDisposable
{
    private readonly GpuDevice _gpuDevice;
    private nint _shaderId;
    private readonly uint _numStorageBuffers;

    private GpuShader(GpuDevice device, nint shaderId, uint numStorageBuffers)
    {
        _gpuDevice = device;
        _shaderId = shaderId;
        _numStorageBuffers = numStorageBuffers;
        device.AddResource(this);
    }

    internal static GpuShader Create(GpuDevice device, GpuShaderCreateInfo info)
    {
        unsafe
        {
            fixed (byte* pCode = info.Code.AsSpan())
            fixed (byte* pEntrypoint = info.Entrypoint.ToUtf8().AsSpan())
            {
                var createInfo = new SDL.GPUShaderCreateInfo
                {
                    CodeSize = (UIntPtr)info.Code.Length,
                    Code = (nint)pCode,
                    Entrypoint = (nint)pEntrypoint,
                    Format = info.Format,
                    Stage = info.Stage,
                    NumSamplers = info.NumSamplers,
                    NumStorageTextures = info.NumStorageTextures,
                    NumStorageBuffers = info.NumStorageBuffers,
                    NumUniformBuffers = info.NumUniformBuffers,
                    Props = info.Properties?.PropertiesId ?? 0
                };

                var shaderId = SDL.CreateGPUShader(device.GpuDeviceID, createInfo);
                return new GpuShader(device, shaderId, info.NumStorageBuffers);
            }
        }
    }

    internal nint ShaderId => _shaderId;

    /// <summary>Storage-buffer slot count this shader expects bound. Used at draw time to skip the bind when the shader doesn't use any.</summary>
    internal uint NumStorageBuffers => _numStorageBuffers;

    public bool IsDisposed => _shaderId == 0;

    public void Dispose()
    {
        var id = Interlocked.Exchange(ref _shaderId, 0);
        if (id != 0)
        {
            _gpuDevice.RemoveResource(this);
            SDL.ReleaseGPUShader(_gpuDevice.GpuDeviceID, _shaderId);
        }
    }
}
