using static SDL3.SDL;

namespace Grape;

/// <summary>
/// A staging buffer used to upload CPU data into GPU resources.
/// </summary>
internal class GpuUploadBuffer : GpuBuffer
{
    private GpuUploadBuffer(GpuDevice device, nint bufferId, uint size)
        : base(device, bufferId, size)
    {
    }

    internal static GpuBuffer Create(GpuDevice device, uint size)
    {
        unsafe
        {
            var info = new GPUTransferBufferCreateInfo { Usage = GPUTransferBufferUsage.Upload, Size = size };
            var transferBufferId = 
                SDL.CreateGPUTransferBuffer(device.GpuDeviceID, info);
            if (transferBufferId == 0)
                throw new InvalidOperationException("Failed to create transfer buffer.");
            return new GpuUploadBuffer(device, transferBufferId, size);
        }
    }

    internal override bool IsTransferBuffer => true;

    /// <summary>
    /// Upload the bytes to the target GPU buffer.
    /// </summary>
    public void Upload(GpuCommandBuffer commandBuffer, GpuBuffer target, ReadOnlySpan<byte> bytes)
    {
        if (commandBuffer.IsDisposed)
            throw new ObjectDisposedException(nameof(commandBuffer));
        if (target.IsDisposed)
            throw new ObjectDisposedException(nameof(target));

        unsafe
        {
            var copyPass = SDL.BeginGPUCopyPass(commandBuffer.CommandBufferId);
            if (copyPass == 0)
                throw new InvalidOperationException($"Failed to begin GPU copy pass: {SDL.GetError()}");

            try
            {
                fixed (byte* pBytes = bytes)
                {
                    var sourceOffset = 0u;
                    var sourceRemaining = (uint)bytes.Length;
                    var targetOffset = 0u;

                    while (sourceRemaining > 0)
                    {
                        void* pMappedBytes = (void*)SDL.MapGPUTransferBuffer(this.Gpu.GpuDeviceID, this.BufferId, true);
                        if (pMappedBytes == null)
                            throw new InvalidOperationException("Failed to map transfer buffer.");

                        uint bytesToCopy = Math.Min(sourceRemaining, this.Size);

                        Buffer.MemoryCopy(pBytes + sourceOffset, pMappedBytes, bytesToCopy, bytesToCopy);
                        SDL.UnmapGPUTransferBuffer(this.Gpu.GpuDeviceID, this.BufferId);

                        SDL.UploadToGPUBuffer(
                            copyPass,
                            new GPUTransferBufferLocation { Offset = 0, TransferBuffer = this.BufferId },
                            new GPUBufferRegion { Buffer = target.BufferId, Offset = targetOffset, Size = bytesToCopy },
                            cycle: true
                        );

                        sourceOffset += bytesToCopy;
                        targetOffset += bytesToCopy;
                        sourceRemaining -= bytesToCopy;
                    }
                }
            }
            finally
            {
                SDL.EndGPUCopyPass(copyPass);
            }
        }
    }
}
