using Blitter.Utilities;

namespace Blitter;

/// <summary>
/// A block of GPU copy and render instructions.
/// </summary>
internal sealed class GpuCommandBuffer : IDisposable
{
    private readonly Pool<GpuCommandBuffer> _pool;
    private nint _commandBufferId;

    // Constructed by the pool factory only. Acquire a real instance via
    // GpuDevice.AllocateCommandBuffer.
    internal GpuCommandBuffer(Pool<GpuCommandBuffer> pool)
    {
        _pool = pool;
    }

    internal nint CommandBufferId => _commandBufferId;

    // Populates a freshly-rented wrapper with the SDL handle. Called by
    // GpuDevice.AllocateCommandBuffer immediately after rent.
    internal void Init(nint commandBufferId)
    {
        _commandBufferId = commandBufferId;
    }

    public bool IsDisposed => _commandBufferId == 0;

    /// <summary>
    /// Returns the wrapper to its pool without cancelling the SDL
    /// command buffer. Used after Submit / SubmitAndAcquireFence has
    /// handed ownership to SDL.
    /// </summary>
    internal void ReleaseWithoutCancel()
    {
        if (_commandBufferId == 0) return;
        _commandBufferId = 0;
        _pool.Return(this);
    }

    public void Dispose()
    {
        var id = _commandBufferId;
        if (id == 0) return;
        _commandBufferId = 0;
        SDL.CancelGPUCommandBuffer(id);
        _pool.Return(this);
    }
}
