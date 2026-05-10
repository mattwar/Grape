namespace Blitter;

using Blitter.Utilities;

/// <summary>
/// A scoped phase for copying data to or from GPU resources.
/// </summary>
internal sealed class GpuCopyPass : IDisposable
{
    private readonly Pool<GpuCopyPass> _pool;
    private nint _copyPassId;

    // Constructed by the pool factory only. Acquire a real instance via
    // GpuDevice.AllocateCopyPass / TryAllocateCopyPass.
    internal GpuCopyPass(Pool<GpuCopyPass> pool)
    {
        _pool = pool;
    }

    internal void Init(nint copyPassId)
    {
        _copyPassId = copyPassId;
    }

    public bool IsDisposed => _copyPassId == 0;

    public void Upload(GpuUploadBuffer source, GpuBuffer destination, ReadOnlySpan<byte> bytes)
    {
        if (IsDisposed) throw new ObjectDisposedException(nameof(GpuCopyPass));
        if (source.IsDisposed)
            throw new ObjectDisposedException(nameof(source));
        if (destination.IsDisposed)
            throw new ObjectDisposedException(nameof(destination));

        unsafe
        {
            fixed (byte* pBytes = bytes)
            {
                void* pMappedBytes = (void*)SDL.MapGPUTransferBuffer(source.Gpu.GpuDeviceID, source.BufferId, true);
                if (pMappedBytes == null)
                    throw new InvalidOperationException("Failed to map transfer buffer.");

                Buffer.MemoryCopy(pBytes, pMappedBytes, bytes.Length, bytes.Length);
                SDL.UnmapGPUTransferBuffer(source.Gpu.GpuDeviceID, source.BufferId);

                SDL.UploadToGPUBuffer(
                    _copyPassId,
                    new SDL.GPUTransferBufferLocation { Offset = 0, TransferBuffer = source.BufferId },
                    new SDL.GPUBufferRegion { Buffer = destination.BufferId, Offset = 0, Size = (uint)bytes.Length },
                    cycle: true
                );
            }
        }
    }

    /// <summary>
    /// Queues a download of a 2D region of <paramref name="source"/>'s
    /// mip 0 layer 0 into <paramref name="destination"/>'s memory. The
    /// download executes when the command buffer is submitted; the
    /// caller must wait on the submit fence before mapping
    /// <paramref name="destination"/>.
    /// </summary>
    public void DownloadFromTexture(
        GpuTexture source,
        GpuDownloadBuffer destination,
        uint width,
        uint height)
    {
        if (IsDisposed) throw new ObjectDisposedException(nameof(GpuCopyPass));
        if (source.IsDisposed)
            throw new ObjectDisposedException(nameof(source));
        if (destination.IsDisposed)
            throw new ObjectDisposedException(nameof(destination));

        SDL.DownloadFromGPUTexture(
            _copyPassId,
            new SDL.GPUTextureRegion
            {
                Texture = source.TextureId,
                MipLevel = 0,
                Layer = 0,
                X = 0,
                Y = 0,
                Z = 0,
                W = width,
                H = height,
                D = 1,
            },
            new SDL.GPUTextureTransferInfo
            {
                TransferBuffer = destination.BufferId,
                Offset = 0,
                PixelsPerRow = width,
                RowsPerLayer = height,
            });
    }

    /// <summary>
    /// Uploads a 2D bitmap region into the destination texture's mip 0 layer 0.
    /// </summary>
    /// <param name="source">Transfer buffer holding the pixel bytes.</param>
    /// <param name="destination">Destination GPU texture.</param>
    /// <param name="width">Region width in pixels.</param>
    /// <param name="height">Region height in pixels.</param>
    /// <param name="bytes">Pixel bytes, tightly packed (PixelsPerRow = width).</param>
    public void UploadToTexture(
        GpuUploadBuffer source,
        GpuTexture destination,
        uint width,
        uint height,
        ReadOnlySpan<byte> bytes)
    {
        if (IsDisposed) throw new ObjectDisposedException(nameof(GpuCopyPass));
        if (source.IsDisposed)
            throw new ObjectDisposedException(nameof(source));
        if (destination.IsDisposed)
            throw new ObjectDisposedException(nameof(destination));

        unsafe
        {
            fixed (byte* pBytes = bytes)
            {
                void* pMappedBytes = (void*)SDL.MapGPUTransferBuffer(source.Gpu.GpuDeviceID, source.BufferId, true);
                if (pMappedBytes == null)
                    throw new InvalidOperationException("Failed to map transfer buffer.");

                Buffer.MemoryCopy(pBytes, pMappedBytes, bytes.Length, bytes.Length);
                SDL.UnmapGPUTransferBuffer(source.Gpu.GpuDeviceID, source.BufferId);

                SDL.UploadToGPUTexture(
                    _copyPassId,
                    new SDL.GPUTextureTransferInfo
                    {
                        TransferBuffer = source.BufferId,
                        Offset = 0,
                        PixelsPerRow = width,
                        RowsPerLayer = height,
                    },
                    new SDL.GPUTextureRegion
                    {
                        Texture = destination.TextureId,
                        MipLevel = 0,
                        Layer = 0,
                        X = 0,
                        Y = 0,
                        Z = 0,
                        W = width,
                        H = height,
                        D = 1,
                    },
                    cycle: true
                );
            }
        }
    }

    /// <summary>
    /// Uploads a 2D bitmap region into a specific layer (and mip level)
    /// of the destination texture. Used for cubemap face uploads, 2D
    /// texture-array slices, and explicit per-mip uploads.
    /// </summary>
    /// <param name="source">Transfer buffer holding the pixel bytes.</param>
    /// <param name="destination">Destination GPU texture.</param>
    /// <param name="width">Region width in pixels.</param>
    /// <param name="height">Region height in pixels.</param>
    /// <param name="layer">Destination layer index (cubemap face index 0..5, array slice).</param>
    /// <param name="mipLevel">Destination mip level (0 is the base level).</param>
    /// <param name="bytes">Pixel bytes, tightly packed (PixelsPerRow = width).</param>
    public void UploadToTextureLayer(
        GpuUploadBuffer source,
        GpuTexture destination,
        uint width,
        uint height,
        uint layer,
        uint mipLevel,
        ReadOnlySpan<byte> bytes)
    {
        if (IsDisposed) throw new ObjectDisposedException(nameof(GpuCopyPass));
        if (source.IsDisposed)
            throw new ObjectDisposedException(nameof(source));
        if (destination.IsDisposed)
            throw new ObjectDisposedException(nameof(destination));

        unsafe
        {
            fixed (byte* pBytes = bytes)
            {
                void* pMappedBytes = (void*)SDL.MapGPUTransferBuffer(source.Gpu.GpuDeviceID, source.BufferId, true);
                if (pMappedBytes == null)
                    throw new InvalidOperationException("Failed to map transfer buffer.");

                Buffer.MemoryCopy(pBytes, pMappedBytes, bytes.Length, bytes.Length);
                SDL.UnmapGPUTransferBuffer(source.Gpu.GpuDeviceID, source.BufferId);

                SDL.UploadToGPUTexture(
                    _copyPassId,
                    new SDL.GPUTextureTransferInfo
                    {
                        TransferBuffer = source.BufferId,
                        Offset = 0,
                        PixelsPerRow = width,
                        RowsPerLayer = height,
                    },
                    new SDL.GPUTextureRegion
                    {
                        Texture = destination.TextureId,
                        MipLevel = mipLevel,
                        Layer = layer,
                        X = 0,
                        Y = 0,
                        Z = 0,
                        W = width,
                        H = height,
                        D = 1,
                    },
                    cycle: false
                );
            }
        }
    }

    public void Dispose()
    {
        var id = _copyPassId;
        if (id == 0) return;
        _copyPassId = 0;
        SDL.EndGPUCopyPass(id);
        _pool.Return(this);
    }
}

