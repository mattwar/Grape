using static SDL3.SDL;

namespace Grape;

/// <summary>
/// A staging buffer used to read GPU data back into CPU memory. Pair
/// with <see cref="GpuCopyPass.DownloadFromTexture"/> to queue a
/// download, then submit + wait the fence before mapping for read.
/// </summary>
internal sealed class GpuDownloadBuffer : GpuBuffer
{
    private GpuDownloadBuffer(GpuDevice device, nint bufferId, uint size)
        : base(device, bufferId, size)
    {
    }

    internal static GpuDownloadBuffer Create(GpuDevice device, uint size)
    {
        var info = new GPUTransferBufferCreateInfo { Usage = GPUTransferBufferUsage.Download, Size = size };
        var transferBufferId = SDL.CreateGPUTransferBuffer(device.GpuDeviceID, info);
        if (transferBufferId == 0)
            throw new InvalidOperationException("Failed to create download transfer buffer.");
        return new GpuDownloadBuffer(device, transferBufferId, size);
    }

    internal override bool IsTransferBuffer => true;

    /// <summary>
    /// Maps the buffer's bytes for reading and copies them into
    /// <paramref name="destination"/>. The fence from the download's
    /// submit must be signaled before this is called, otherwise the
    /// bytes may be incomplete.
    /// </summary>
    public unsafe void Read(Span<byte> destination)
    {
        if (IsDisposed)
            throw new ObjectDisposedException(nameof(GpuDownloadBuffer));
        if (destination.Length > this.Size)
            throw new ArgumentException(
                $"Destination span ({destination.Length} bytes) is larger than this download buffer ({this.Size} bytes).",
                nameof(destination));

        void* pMapped = (void*)SDL.MapGPUTransferBuffer(this.Gpu.GpuDeviceID, this.BufferId, false);
        if (pMapped == null)
            throw new InvalidOperationException("Failed to map download transfer buffer.");

        try
        {
            fixed (byte* pDest = destination)
                Buffer.MemoryCopy(pMapped, pDest, destination.Length, destination.Length);
        }
        finally
        {
            SDL.UnmapGPUTransferBuffer(this.Gpu.GpuDeviceID, this.BufferId);
        }
    }
}
