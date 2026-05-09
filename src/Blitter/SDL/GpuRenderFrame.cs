using System.Collections.Immutable;

namespace Blitter;

/// <summary>
/// A single frame of GPU work, including copy and render passes.
/// </summary>
internal sealed class GpuRenderFrame : IDisposable
{
    private readonly GpuDevice _device;
    private readonly GpuCommandBuffer _commandBuffer;
    private bool _disposed;

    internal GpuRenderFrame(GpuDevice device, GpuCommandBuffer commandBuffer)
    {
        _device = device;
        _commandBuffer = commandBuffer;
    }

    internal GpuCommandBuffer CommandBuffer => _commandBuffer;

    public GpuCopyPass BeginCopyPass() =>
        GpuCopyPass.Begin(_commandBuffer);

    /// <summary>
    /// Attempts to begin a copy pass. Returns <c>false</c> (without throwing)
    /// if the underlying SDL call fails, for example because the device is
    /// being torn down or the window has just been closed.
    /// </summary>
    public bool TryBeginCopyPass(out GpuCopyPass? copyPass) =>
        GpuCopyPass.TryBegin(_commandBuffer, out copyPass);

    public GpuRenderPass BeginRenderPass(
        ImmutableArray<GpuColorTargetInfo> colorTargets,
        GpuDepthStencilTargetInfo depthTarget) =>
        _device.BeginRenderPass(_commandBuffer, colorTargets, depthTarget);

    /// <summary>
    /// Attempts to begin a render pass. Returns <c>false</c> (without throwing)
    /// if the underlying SDL call fails.
    /// </summary>
    public bool TryBeginRenderPass(
        ImmutableArray<GpuColorTargetInfo> colorTargets,
        GpuDepthStencilTargetInfo depthTarget,
        out GpuRenderPass? renderPass) =>
        _device.TryBeginRenderPass(_commandBuffer, colorTargets, depthTarget, out renderPass);

    public void Submit()
    {
        if (_disposed)
            return;

        _disposed = true;
        SDL.SubmitGPUCommandBuffer(_commandBuffer.CommandBufferId);
        _commandBuffer.ReleaseWithoutCancel();
    }

    public GpuFence SubmitAndAcquireFence()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(GpuRenderFrame));

        _disposed = true;
        var fence = SDL.SubmitGPUCommandBufferAndAcquireFence(_commandBuffer.CommandBufferId);
        _commandBuffer.ReleaseWithoutCancel();
        return new GpuFence(_device, fence);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _commandBuffer.Dispose();
        }
    }
}
