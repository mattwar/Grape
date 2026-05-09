namespace Blitter;

/// <summary>
/// A join point for GPU work.
/// </summary>
internal sealed class GpuFence : IDisposable
{
    private readonly GpuDevice _device;
    private nint _fenceId;

    internal GpuFence(GpuDevice device, nint fenceId)
    {
        _device = device;
        _fenceId = fenceId;
    }

    public bool IsDisposed => _fenceId == 0;

    public bool IsSignaled => !IsDisposed && SDL.QueryGPUFence(_device.GpuDeviceID, _fenceId);

    /// <summary>
    /// Blocks the calling thread until this fence is signaled (i.e. the
    /// submitted command buffer has finished executing on the GPU).
    /// Returns immediately if already signaled or disposed.
    /// </summary>
    public void Wait()
    {
        if (IsDisposed)
            return;
        SDL.WaitForGPUFences(_device.GpuDeviceID, true, [_fenceId], 1);
    }

    public void Dispose()
    {
        var id = Interlocked.Exchange(ref _fenceId, 0);
        if (id != 0)
        {
            SDL.ReleaseGPUFence(_device.GpuDeviceID, id);
        }
    }
}
