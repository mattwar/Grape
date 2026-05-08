namespace Grape;

/// <summary>
/// A block of GPU copy and render instructions.
/// </summary>
internal sealed class GpuCommandBuffer: IDisposable
{
    private readonly GpuDevice _gpuDevice;
    private nint _commandBufferId;

    private GpuCommandBuffer(GpuDevice device, nint commandBufferId)
    {
        _gpuDevice = device;
        _commandBufferId = commandBufferId;
        device.AddResource(this);
    }

    internal nint CommandBufferId => _commandBufferId;

    internal static GpuCommandBuffer Create(GpuDevice device)
    {
        var commandBufferId = SDL.AcquireGPUCommandBuffer(device.GpuDeviceID);
        return new GpuCommandBuffer(device, commandBufferId);
    }

    public bool IsDisposed => _commandBufferId == 0;

    /// <summary>
    /// Releases the wrapper without cancelling the underlying command buffer.
    /// Call this once the buffer has been handed off to SDL via Submit/SubmitAndAcquireFence.
    /// </summary>
    internal void ReleaseWithoutCancel()
    {
        var id = Interlocked.Exchange(ref _commandBufferId, 0);
        if (id != 0)
        {
            _gpuDevice.RemoveResource(this);
        }
    }

    public void Dispose()
    {
        var id = Interlocked.Exchange(ref _commandBufferId, 0);
        if (id != 0)
        {
            SDL.CancelGPUCommandBuffer(id);
            _gpuDevice.RemoveResource(this);
        }
    }
}
