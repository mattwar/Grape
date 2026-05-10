using System.Collections.Immutable;
using Blitter.Utilities;

namespace Blitter;

/// <summary>
/// A single frame of GPU work, including copy and render passes.
/// </summary>
internal sealed class GpuRenderFrame : IDisposable
{
    private readonly GpuDevice _device;
    private readonly Pool<GpuRenderFrame> _pool;
    private GpuCommandBuffer? _commandBuffer;
    // Tracks whether the command buffer has already been handed off to
    // SDL via Submit / SubmitAndAcquireFence so Dispose knows not to
    // cancel it.
    private bool _submitted;

    // Constructed by the pool factory only. Acquire a real instance via
    // GpuDevice.AllocateRenderFrame (called from GpuDevice.BeginFrame).
    internal GpuRenderFrame(GpuDevice device, Pool<GpuRenderFrame> pool)
    {
        _device = device;
        _pool = pool;
    }

    internal void Init(GpuCommandBuffer commandBuffer)
    {
        _commandBuffer = commandBuffer;
        _submitted = false;
    }

    internal GpuCommandBuffer CommandBuffer => _commandBuffer
        ?? throw new ObjectDisposedException(nameof(GpuRenderFrame));

    public GpuCopyPass BeginCopyPass() =>
        _device.AllocateCopyPass(CommandBuffer);

    /// <summary>
    /// Attempts to begin a copy pass. Returns <c>false</c> (without throwing)
    /// if the underlying SDL call fails, for example because the device is
    /// being torn down or the window has just been closed.
    /// </summary>
    public bool TryBeginCopyPass(out GpuCopyPass? copyPass) =>
        _device.TryAllocateCopyPass(CommandBuffer, out copyPass);

    public GpuRenderPass BeginRenderPass(
        ReadOnlySpan<GpuColorTargetInfo> colorTargets,
        GpuDepthStencilTargetInfo depthTarget) =>
        _device.AllocateRenderPass(CommandBuffer, colorTargets, depthTarget);

    /// <summary>
    /// Attempts to begin a render pass. Returns <c>false</c> (without throwing)
    /// if the underlying SDL call fails.
    /// </summary>
    public bool TryBeginRenderPass(
        ReadOnlySpan<GpuColorTargetInfo> colorTargets,
        GpuDepthStencilTargetInfo depthTarget,
        out GpuRenderPass? renderPass) =>
        _device.TryAllocateRenderPass(CommandBuffer, colorTargets, depthTarget, out renderPass);

    public void Submit()
    {
        if (_submitted || _commandBuffer is null) return;

        _submitted = true;
        SDL.SubmitGPUCommandBuffer(_commandBuffer.CommandBufferId);
        _commandBuffer.ReleaseWithoutCancel();
    }

    public GpuFence SubmitAndAcquireFence()
    {
        if (_submitted || _commandBuffer is null)
            throw new ObjectDisposedException(nameof(GpuRenderFrame));

        _submitted = true;
        var fenceId = SDL.SubmitGPUCommandBufferAndAcquireFence(_commandBuffer.CommandBufferId);
        _commandBuffer.ReleaseWithoutCancel();
        return _device.AllocateFence(fenceId);
    }

    public void Dispose()
    {
        if (_commandBuffer is null) return;

        // If Submit wasn't called, the command buffer needs to be
        // cancelled (its Dispose returns it to the pool). After Submit
        // the buffer was already released to the pool without cancel,
        // so we just clear our reference.
        if (!_submitted)
            _commandBuffer.Dispose();
        _commandBuffer = null;
        _submitted = false;
        _pool.Return(this);
    }
}
