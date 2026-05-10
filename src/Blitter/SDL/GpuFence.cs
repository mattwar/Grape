namespace Blitter;

using Blitter.Utilities;

/// <summary>
/// A join point for GPU work.
/// </summary>
internal sealed class GpuFence : IDisposable
{
    private readonly GpuDevice _device;
    private readonly Pool<GpuFence> _pool;
    private nint _fenceId;

    // Constructed by the pool factory only. Acquire a real instance via
    // GpuDevice.AllocateFence (called from GpuRenderFrame.SubmitAndAcquireFence).
    internal GpuFence(GpuDevice device, Pool<GpuFence> pool)
    {
        _device = device;
        _pool = pool;
    }

    internal void Init(nint fenceId)
    {
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
        // Marshal the single fence id through a stack-allocated span to
        // avoid the per-call array allocation of a `[_fenceId]` collection
        // expression.
        unsafe
        {
            var id = _fenceId;
            SDL.WaitForGPUFences(_device.GpuDeviceID, true, (nint)(&id), 1);
        }
    }

    public void Dispose()
    {
        var id = _fenceId;
        if (id == 0) return;
        _fenceId = 0;
        SDL.ReleaseGPUFence(_device.GpuDeviceID, id);
        _pool.Return(this);
    }
}
