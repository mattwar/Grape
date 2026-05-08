using System.Collections.Immutable;
using static SDL3.SDL;

namespace Grape;

/// <summary>
/// Corresponds to the GPU hardware device.
/// </summary>
internal sealed class GpuDevice : IDisposable
{
    private ImmutableList<IDisposable> _resources = ImmutableList<IDisposable>.Empty;

    private nint _gpuDeviceID;

    internal GpuDevice(nint gpuDeviceID)
    {
        _gpuDeviceID = gpuDeviceID;
        Application.Current.AddResource(this);
    }

    public static GpuDevice Create(SDL.GPUShaderFormat format, bool debugMode, string? name = null)
    {
        // Make sure the SDL video subsystem is up before asking for a GPU
        // device. Touching Application.Current starts it on demand.
        _ = Application.Current;
        if (!SDL.InitSubSystem(SDL.InitFlags.Video))
            throw new InvalidOperationException(
                $"Failed to initialize SDL video subsystem: {SDL.GetError()}");

        var id = SDL.CreateGPUDevice(format, debugMode, name);
        if (id == 0)
            throw new InvalidOperationException(
                $"Failed to create GPU device for shader format(s) {format}: {SDL.GetError()}");
        return new GpuDevice(id);
    }

    private static GpuDevice? _default;

    /// <summary>
    /// A lazily-created shared GPU device using all supported shader formats.
    /// Created on first access and disposed with the <see cref="Application"/>.
    /// </summary>
    public static GpuDevice Default =>
        _default ??= Create(
            SDL.GPUShaderFormat.SPIRV | SDL.GPUShaderFormat.DXIL | SDL.GPUShaderFormat.MSL,
            debugMode: false);

    internal nint GpuDeviceID => _gpuDeviceID;

    public bool IsDisposed => _gpuDeviceID == 0;

    public void Dispose()
    {
        var id = Interlocked.Exchange(ref _gpuDeviceID, 0);
        if (id != 0)
        {
            foreach (var resource in _resources)
            {
                resource.Dispose();
            }

            SDL.DestroyGPUDevice(id);
            Application.Current.RemoveResource(this);
        }
    }

    internal void AddResource(IDisposable resource)
    {
        ImmutableInterlocked.Update(ref _resources, (old) => old.Add(resource));
    }

    internal void RemoveResource(IDisposable resource)
    {
        ImmutableInterlocked.Update(ref _resources, (old) => old.Remove(resource));
    }

    /// <summary>
    /// The shader format supported by this GPU device.
    /// </summary>
    public SDL.GPUShaderFormat ShaderFormat =>
        !IsDisposed
            ? SDL.GetGPUShaderFormats(_gpuDeviceID)
            : SDL.GPUShaderFormat.Invalid;

    /// <summary>
    /// The name of the GPU's driver.
    /// </summary>
    public string Driver =>
        !IsDisposed
            ? SDL.GetGPUDeviceDriver(_gpuDeviceID) ?? ""
            : "";

    /// <summary>
    /// The available GPU drivers.
    /// </summary>
    public ImmutableList<string> Drivers
    {
        get
        {
            if (_drivers == null)
            {
                int count = SDL.GetNumGPUDrivers();
                var drivers = Enumerable.Range(0, count).Select(i => SDL.GetGPUDriver(i)).ToImmutableList();
                ImmutableInterlocked.Update(ref _drivers, _old => _old == null ? drivers : _old);
            }
            return _drivers!;
        }
    }

    private ImmutableList<string>? _drivers;

    /// <summary>
    /// Creates a new command buffer on the GPU.
    /// </summary>
    public GpuCommandBuffer CreateCommandBuffer() => 
        GpuCommandBuffer.Create(this);

    /// <summary>
    /// Begins a frame of GPU work.
    /// </summary>
    public GpuRenderFrame BeginFrame() =>
        new GpuRenderFrame(this, CreateCommandBuffer());

    /// <summary>
    /// Creates a new shader for the GPU.
    /// </summary>
    public GpuShader CreateShader(GpuShaderCreateInfo info) =>
        GpuShader.Create(this, info);

    /// <summary>
    /// Creates a new texture on the GPU.
    /// </summary>
    public GpuTexture CreateTexture(GpuTextureCreateInfo info) =>
        GpuTexture.Create(this, info);

    /// <summary>
    /// Creates a new sampler on the GPU.
    /// </summary>
    public GpuSampler CreateSampler(GpuSamplerCreateInfo info) =>
        GpuSampler.Create(this, info);

    /// <summary>
    /// Creates a new vertex buffer on the GPU.
    /// </summary>
    public GpuVertexBuffer<TVertex> CreateVertexBuffer<TVertex>(uint size, GpuVertexBufferLayout layout)
        where TVertex : unmanaged
        =>
        GpuVertexBuffer<TVertex>.Create(this, size, layout);

    /// <summary>
    /// Creates a new index buffer on the GPU. Indices are 32-bit unsigned integers.
    /// </summary>
    public GpuIndexBuffer CreateIndexBuffer(uint size) =>
        GpuIndexBuffer.Create(this, size);

    /// <summary>
    /// Create a new render pass for the GPU.
    /// </summary>
    public GpuRenderPass BeginRenderPass(
        GpuCommandBuffer commandBuffer, 
        ImmutableArray<GpuColorTargetInfo> colorTargets, 
        GpuDepthStencilTargetInfo depthTarget
        ) =>
        GpuRenderPass.Begin(this, commandBuffer, colorTargets, depthTarget);

    /// <summary>
    /// Attempts to begin a render pass. Returns <c>false</c> (without throwing)
    /// if the underlying SDL call fails, e.g. because the device is being torn
    /// down or the swapchain image is no longer valid.
    /// </summary>
    public bool TryBeginRenderPass(
        GpuCommandBuffer commandBuffer,
        ImmutableArray<GpuColorTargetInfo> colorTargets,
        GpuDepthStencilTargetInfo depthTarget,
        out GpuRenderPass? renderPass) =>
        GpuRenderPass.TryBegin(this, commandBuffer, colorTargets, depthTarget, out renderPass);

    /// <summary>
    /// Create a new graphics pipeline for the GPU.
    /// </summary>
    public GpuPipeline CreateGraphicsPipeline(GpuPipelineCreateInfo info) =>
        GpuPipeline.CreateGraphicsPipeline(this, info);
}

/// <summary>
/// A GPU texture resource that can be used as render target or sampled data.
/// </summary>
internal sealed class GpuTexture : IDisposable
{
    private readonly GpuDevice? _gpuDevice;
    private nint _textureId;
    private readonly bool _owned;

    private GpuTexture(GpuDevice device, nint textureId)
    {
        _gpuDevice = device;
        _textureId = textureId;
        _owned = true;
        device.AddResource(this);
    }

    private GpuTexture(nint textureId)
    {
        _gpuDevice = null;
        _textureId = textureId;
        _owned = false;
    }

    /// <summary>
    /// Wraps a texture handle whose lifetime is owned by something else (e.g.
    /// a swapchain). Disposing this wrapper does not release the underlying
    /// texture.
    /// </summary>
    internal static GpuTexture WrapBorrowed(nint textureId) => new GpuTexture(textureId);

    internal static GpuTexture Create(GpuDevice device, GpuTextureCreateInfo info)
    {
        var nativeInfo = new SDL.GPUTextureCreateInfo
        {
            Type = info.Type,
            Format = info.Format,
            Usage = info.Usage,
            Width = info.Width,
            Height = info.Height,
            LayerCountOrDepth = info.LayerCountOrDepth,
            NumLevels = info.NumLevels,
            SampleCount = info.SampleCount,
            Props = info.Properties?.PropertiesId ?? 0
        };

        var textureId = SDL.CreateGPUTexture(device.GpuDeviceID, nativeInfo);
        return new GpuTexture(device, textureId);
    }

    internal nint TextureId => _textureId;

    public bool IsDisposed => _textureId == 0;

    public void Dispose()
    {
        var id = Interlocked.Exchange(ref _textureId, 0);
        if (id != 0 && _owned && _gpuDevice is not null)
        {
            _gpuDevice.RemoveResource(this);
            SDL.ReleaseGPUTexture(_gpuDevice.GpuDeviceID, id);
        }
    }
}

/// <summary>
/// A GPU sampler resource that controls how textures are sampled by shaders.
/// </summary>
internal sealed class GpuSampler : IDisposable
{
    private readonly GpuDevice _gpuDevice;
    private nint _samplerId;

    private GpuSampler(GpuDevice device, nint samplerId)
    {
        _gpuDevice = device;
        _samplerId = samplerId;
        device.AddResource(this);
    }

    internal static GpuSampler Create(GpuDevice device, GpuSamplerCreateInfo info)
    {
        var nativeInfo = new SDL.GPUSamplerCreateInfo
        {
            MinFilter = info.MinFilter,
            MagFilter = info.MagFilter,
            MipmapMode = info.MipmapMode,
            AddressModeU = info.AddressModeU,
            AddressModeV = info.AddressModeV,
            AddressModeW = info.AddressModeW,
            MipLodBias = info.MipLodBias,
            MaxAnisotropy = info.MaxAnisotropy,
            CompareOp = info.CompareOp,
            MinLod = info.MinLod,
            MaxLod = info.MaxLod,
            EnableAnisotropy = (byte)(info.EnableAnisotropy ? 1 : 0),
            EnableCompare = (byte)(info.EnableCompare ? 1 : 0),
        };

        var samplerId = SDL.CreateGPUSampler(device.GpuDeviceID, nativeInfo);
        return new GpuSampler(device, samplerId);
    }

    internal nint SamplerId => _samplerId;

    public bool IsDisposed => _samplerId == 0;

    public void Dispose()
    {
        var id = Interlocked.Exchange(ref _samplerId, 0);
        if (id != 0)
        {
            _gpuDevice.RemoveResource(this);
            SDL.ReleaseGPUSampler(_gpuDevice.GpuDeviceID, id);
        }
    }
}

/// <summary>
/// Describes a GPU sampler to be created.
/// </summary>
internal record GpuSamplerCreateInfo
{
    public SDL.GPUFilter MinFilter { get; init; }
    public SDL.GPUFilter MagFilter { get; init; }
    public SDL.GPUSamplerMipmapMode MipmapMode { get; init; }
    public SDL.GPUSamplerAddressMode AddressModeU { get; init; }
    public SDL.GPUSamplerAddressMode AddressModeV { get; init; }
    public SDL.GPUSamplerAddressMode AddressModeW { get; init; }
    public float MipLodBias { get; init; }
    public float MaxAnisotropy { get; init; }
    public SDL.GPUCompareOp CompareOp { get; init; }
    public float MinLod { get; init; }
    public float MaxLod { get; init; }
    public bool EnableAnisotropy { get; init; }
    public bool EnableCompare { get; init; }
}

internal record GpuTextureCreateInfo
{
    // GPUTextureCreateInfo;

    /// <summary>
    /// The base dimensionality of the texture.
    /// </summary>
    public GPUTextureType Type { get; init; }

    /// <summary>
    /// The pixel format of the texture.
    /// </summary>
    public GPUTextureFormat Format { get; init; }

    /// <summary>
    /// How the texture is intended to be used by the client.
    /// </summary>
    public GPUTextureUsageFlags Usage { get; init; }

    /// <summary>
    /// The width of the texture.
    /// </summary>
    public UInt32 Width { get; init; }

    /// <summary>
    /// The height of the texture.
    /// </summary>
    public UInt32 Height { get; init; }

    /// <summary>
    /// The layer count or depth of the texture. This value is treated as a layer count on 2D array textures, and as a depth value on 3D textures.
    /// </summary>
    public UInt32 LayerCountOrDepth { get; init; }

    /// <summary>
    /// The number of mip levels in the texture.
    /// </summary>
    public UInt32 NumLevels { get; init; }

    /// <summary>
    /// The number of samples per texel. Only applies if the texture is used as a render target.
    /// </summary>
    public GPUSampleCount SampleCount { get; init; }

    /// <summary>
    /// A properties ID for extensions. Should be 0 if no extensions are needed.
    /// </summary>
    public Properties? Properties { get; init; }
}

/// <summary>
/// A GPU buffer resource used to store raw data on the device.
/// </summary>
internal abstract class GpuBuffer : IDisposable
{
    private readonly GpuDevice _gpuDevice;
    private nint _bufferId;
    private uint _size;

    private protected GpuBuffer(GpuDevice device, nint bufferId, uint size)
    {
        _gpuDevice = device;
        _bufferId = bufferId;
        _size = size;
        device.AddResource(this);
    }

    public GpuDevice Gpu => _gpuDevice;
    public uint Size => _size;
    internal nint BufferId => _bufferId;
    internal virtual bool IsTransferBuffer => false;

    public bool IsDisposed => _bufferId == 0;

    public void Dispose()
    {
        var id = Interlocked.Exchange(ref _bufferId, 0);
        if (id != 0)
        {
            _gpuDevice.RemoveResource(this);
            if (IsTransferBuffer)
            {
                SDL.ReleaseGPUTransferBuffer(_gpuDevice.GpuDeviceID, id);
            }
            else
            {
                SDL.ReleaseGPUBuffer(_gpuDevice.GpuDeviceID, id);
            }
        }
    }

    /// <summary>
    /// The name of the buffer.
    /// </summary>
    public string Name
    {
        get => _name ?? "";
        set
        {
            if (IsDisposed)
                return;
            _name = value;
            SDL.SetGPUBufferName(_gpuDevice.GpuDeviceID, _bufferId, value);
        }
    }

    private string? _name;
}

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

/// <summary>
/// A scoped phase for copying data to or from GPU resources.
/// </summary>
internal sealed class GpuCopyPass : IDisposable
{
    private readonly nint _copyPassId;
    private bool _disposed;

    private GpuCopyPass(nint copyPassId)
    {
        _copyPassId = copyPassId;
    }

    internal static GpuCopyPass Begin(GpuCommandBuffer commandBuffer)
    {
        if (!TryBegin(commandBuffer, out var copyPass) || copyPass == null)
            throw new InvalidOperationException($"Failed to begin GPU copy pass: {SDL.GetError()}");
        return copyPass;
    }

    /// <summary>
    /// Attempts to begin a copy pass on the given command buffer. Returns
    /// <c>false</c> (with <paramref name="copyPass"/> set to <c>null</c>) if
    /// the underlying SDL call fails, e.g. because the device is being torn
    /// down. Does not throw.
    /// </summary>
    internal static bool TryBegin(GpuCommandBuffer commandBuffer, out GpuCopyPass? copyPass)
    {
        var copyPassId = SDL.BeginGPUCopyPass(commandBuffer.CommandBufferId);
        if (copyPassId == 0)
        {
            copyPass = null;
            return false;
        }

        copyPass = new GpuCopyPass(copyPassId);
        return true;
    }

    public void Upload(GpuUploadBuffer source, GpuBuffer destination, ReadOnlySpan<byte> bytes)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(GpuCopyPass));
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
                    new GPUTransferBufferLocation { Offset = 0, TransferBuffer = source.BufferId },
                    new GPUBufferRegion { Buffer = destination.BufferId, Offset = 0, Size = (uint)bytes.Length },
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
        if (_disposed)
            throw new ObjectDisposedException(nameof(GpuCopyPass));
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
        if (_disposed)
            throw new ObjectDisposedException(nameof(GpuCopyPass));
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
        if (_disposed)
            throw new ObjectDisposedException(nameof(GpuCopyPass));
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
        if (!_disposed)
        {
            _disposed = true;
            SDL.EndGPUCopyPass(_copyPassId);
        }
    }
}

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

/// <summary>
/// Describes how vertex data is laid out inside a vertex buffer.
/// </summary>
internal record GpuVertexBufferLayout
{
    /// <summary>
    /// The size of a single vertex + the offset between vertices.
    /// </summary>
    public int Pitch { get; init; }

    /// <summary>
    /// The description of the data elements of each vertex.
    /// </summary>
    public ImmutableArray<GpuShaderVertexElement> Elements { get; init; }

    /// <summary>
    /// Whether attribute addressing is a function of the vertex index or instance index.
    /// </summary>
    public GPUVertexInputRate InputRate { get; init; } = GPUVertexInputRate.Vertex;
}

/// <summary>
/// Describes one element within a vertex layout.
/// </summary>
internal record GpuShaderVertexElement
{
    /// <summary>
    /// The size and type of the attribute data.
    /// </summary>
    public GPUVertexElementFormat Format { get; init; }

    /// <summary>
    /// The byte offset of this attribute relative to the start of the vertex element.
    /// </summary>
    public UInt32 Offset { get; init; }
}

/// <summary>
/// A GPU buffer that is intended for vertex input.
/// </summary>
internal abstract class GpuVertexBuffer : GpuBuffer
{
    private protected GpuVertexBuffer(GpuDevice device, nint bufferId, uint size, GpuVertexBufferLayout layout)
        : base(device, bufferId, size)
    {
        this.Layout = layout;
    }

    public GpuVertexBufferLayout Layout { get; }

}

/// <summary>
/// A typed vertex buffer for structured vertex data.
/// </summary>
internal class GpuVertexBuffer<TVertex> : GpuVertexBuffer
    where TVertex : unmanaged
{
    private GpuVertexBuffer(GpuDevice device, nint bufferId, uint size, GpuVertexBufferLayout layout)
        : base(device, bufferId, size, layout)
    {
    }

    internal static GpuVertexBuffer<TVertex> Create(
        GpuDevice device,
        uint size,
        GpuVertexBufferLayout layout)
    {
        unsafe
        {
            var createInfo = new SDL.GPUBufferCreateInfo
            {
                Size = size,
                Usage = SDL.GPUBufferUsageFlags.Vertex,
                Props = 0
            };

            var bufferId = SDL.CreateGPUBuffer(device.GpuDeviceID, createInfo);
            return new GpuVertexBuffer<TVertex>(device, bufferId, createInfo.Size, layout);
        }
    }

    public void Upload(GpuCommandBuffer commandBuffer, Span<TVertex> vertices, GpuUploadBuffer uploadBuffer)
    {
        unsafe
        {
            fixed (void* pVertices = vertices)
            {
                var byteSpan = new ReadOnlySpan<byte>(pVertices, vertices.Length * sizeof(TVertex));
                uploadBuffer.Upload(commandBuffer, this, byteSpan);
            }
        }
    }
}

/// <summary>
/// A GPU buffer that is intended for indexed-primitive draws. Holds an
/// array of 32-bit unsigned integers indexing into a vertex buffer.
/// </summary>
internal sealed class GpuIndexBuffer : GpuBuffer
{
    private GpuIndexBuffer(GpuDevice device, nint bufferId, uint size)
        : base(device, bufferId, size)
    {
    }

    internal static GpuIndexBuffer Create(GpuDevice device, uint size)
    {
        var createInfo = new SDL.GPUBufferCreateInfo
        {
            Size = size,
            Usage = SDL.GPUBufferUsageFlags.Index,
            Props = 0,
        };

        var bufferId = SDL.CreateGPUBuffer(device.GpuDeviceID, createInfo);
        return new GpuIndexBuffer(device, bufferId, createInfo.Size);
    }
}

/// <summary>
/// A GPU storage buffer (a.k.a. SSBO / structured buffer) that vertex
/// or fragment shaders can read from. Used for resources whose count
/// isn't known at shader-compile time (e.g. a variable-length list of
/// point lights). Pair with <c>StructuredBuffer&lt;T&gt;</c> in HLSL.
/// </summary>
internal sealed class GpuStorageBuffer : GpuBuffer
{
    private GpuStorageBuffer(GpuDevice device, nint bufferId, uint size)
        : base(device, bufferId, size)
    {
    }

    internal static GpuStorageBuffer Create(GpuDevice device, uint size)
    {
        var createInfo = new SDL.GPUBufferCreateInfo
        {
            Size = size,
            Usage = SDL.GPUBufferUsageFlags.GraphicsStorageRead,
            Props = 0,
        };

        var bufferId = SDL.CreateGPUBuffer(device.GpuDeviceID, createInfo);
        if (bufferId == 0)
            throw new InvalidOperationException(
                $"Failed to create GPU storage buffer: {SDL.GetError()}");
        return new GpuStorageBuffer(device, bufferId, createInfo.Size);
    }
}


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

/// <summary>
/// A scoped render recording phase for drawing into one or more GPU targets.
/// </summary>
internal sealed class GpuRenderPass : IDisposable
{
    private readonly GpuDevice _gpuDevice;
    private readonly GpuCommandBuffer _gpuCommandBuffer;
    private nint _gpuRenderPassID;

    private GpuRenderPass(GpuDevice device, GpuCommandBuffer commandBuffer, nint gpuRenderPassID)
    {
        _gpuDevice = device;
        _gpuCommandBuffer = commandBuffer;
        _gpuRenderPassID = gpuRenderPassID;
        device.AddResource(this);
    }

    public bool IsDisposed => _gpuRenderPassID == 0;

    public void Dispose()
    {
        var id = Interlocked.Exchange(ref _gpuRenderPassID, 0);
        if (id != 0)
        {
            SDL.EndGPURenderPass(id);
            _gpuDevice.RemoveResource(this);
        }
    }

    internal static GpuRenderPass Begin(
        GpuDevice device,
        GpuCommandBuffer commandBuffer,
        IReadOnlyList<GpuColorTargetInfo> colorTargets,
        GpuDepthStencilTargetInfo depthTarget)
    {
        var renderPassId = BeginNative(commandBuffer, colorTargets, depthTarget);
        if (renderPassId == 0)
            throw new InvalidOperationException($"Failed to begin GPU render pass: {SDL.GetError()}");
        return new GpuRenderPass(device, commandBuffer, renderPassId);
    }

    /// <summary>
    /// Attempts to begin a render pass. Returns <c>false</c> (without throwing)
    /// if the underlying SDL call fails, for example because the device is
    /// being torn down or the swapchain image is no longer valid.
    /// </summary>
    internal static bool TryBegin(
        GpuDevice device,
        GpuCommandBuffer commandBuffer,
        IReadOnlyList<GpuColorTargetInfo> colorTargets,
        GpuDepthStencilTargetInfo depthTarget,
        out GpuRenderPass? renderPass)
    {
        var renderPassId = BeginNative(commandBuffer, colorTargets, depthTarget);
        if (renderPassId == 0)
        {
            renderPass = null;
            return false;
        }

        renderPass = new GpuRenderPass(device, commandBuffer, renderPassId);
        return true;
    }

    private static nint BeginNative(
        GpuCommandBuffer commandBuffer,
        IReadOnlyList<GpuColorTargetInfo> colorTargets,
        GpuDepthStencilTargetInfo depthTarget)
    {
        // allocate space for target infos on stack.
        // assumption is that this is consumed in BeginGPURenderPass and not stored.
        Span<GPUColorTargetInfo> nativeColorTargets = stackalloc GPUColorTargetInfo[colorTargets.Count];
        for (int i = 0; i < colorTargets.Count; i++)
        {
            var ct = colorTargets[i];
            nativeColorTargets[i] = new SDL.GPUColorTargetInfo
            {
                Texture = ct.Texture?.TextureId ?? 0,
                MipLevel = ct.MipLevel,
                LayerOrDepthPlane = ct.LayerOrDepthPlane,
                ClearColor = ct.ClearColor,
                LoadOp = ct.LoadOp,
                StoreOp = ct.StoreOp,
                ResolveTexture = ct.ResolveTexture?.TextureId ?? 0,
                ResolveMipLevel = ct.ResolveMipLevel,
                ResolveLayer = ct.ResolveLayer,
                Cycle = (byte)(ct.Cycle ? 1 : 0),
                CycleResolveTexture = (byte)(ct.CycleResolveTexture ? 1 : 0)
            };
        }

        var nativeDepthTarget = new SDL.GPUDepthStencilTargetInfo
        {
            Texture = depthTarget.Texture?.TextureId ?? 0,
            ClearDepth = depthTarget.ClearDepth,
            LoadOp = depthTarget.LoadOp,
            StoreOp = depthTarget.StoreOp,
            StencilLoadOp = depthTarget.StencilLoadOp,
            StencilStoreOp = depthTarget.StencilStoreOp,
            Cycle = (byte)(depthTarget.Cycle ? 1 : 0),
            ClearStencil = depthTarget.ClearStencil
        };

        unsafe
        {
            fixed (GPUColorTargetInfo* pColorTargets = nativeColorTargets)
            {
                if (depthTarget.Texture is null)
                {
                    // SDL_GPU expects a NULL pointer when no depth/stencil
                    // target is in use. Passing a zero-filled struct by ref
                    // is NOT equivalent and causes native heap corruption.
                    return SDL.BeginGPURenderPass(
                        commandBuffer.CommandBufferId,
                        (nint)pColorTargets,
                        (uint)nativeColorTargets.Length,
                        IntPtr.Zero);
                }

                return SDL.BeginGPURenderPass(
                    commandBuffer.CommandBufferId,
                    (nint)pColorTargets,
                    (uint)nativeColorTargets.Length,
                    nativeDepthTarget);
            }
        }
    }

    /// <summary>
    /// Binds the graphics pipeline to the render pass's command buffer.
    /// </summary>
    public void BindGraphicsPipeline(GpuPipeline pipeline)
    {
        if (IsDisposed)
            throw new ObjectDisposedException(nameof(GpuRenderPass));
        if (pipeline.IsDisposed)
            throw new ObjectDisposedException(nameof(pipeline));
        SDL.BindGPUGraphicsPipeline(_gpuRenderPassID, pipeline.PipelineId);
    }

    /// <summary>
    /// Binds vertex buffers to the render pass's command buffer.
    /// </summary>
    public void BindVertexBuffers(ReadOnlySpan<GpuBuffer> buffers)
    {
        unsafe
        {
            Span<SDL.GPUBufferBinding> bindings = stackalloc SDL.GPUBufferBinding[buffers.Length];
            fixed (SDL.GPUBufferBinding* pBindings = bindings)
            {
                for (int i = 0; i < buffers.Length; i++)
                {
                    var buffer = buffers[i];
                    if (buffer.IsDisposed)
                        throw new ObjectDisposedException(nameof(buffer));
                    bindings[i] = new SDL.GPUBufferBinding
                    {
                        Buffer = buffer.BufferId,
                        Offset = 0
                    };
                }
                SDL.BindGPUVertexBuffers(_gpuRenderPassID, 0U, (nint)pBindings, (uint)buffers.Length);
            }
        }
    }

    /// <summary>
    /// Binds an index buffer to the render pass's command buffer.
    /// </summary>
    public void BindIndexBuffer(GpuBuffer buffer, SDL.GPUIndexElementSize indexElementSize, uint offset = 0)
    {
        if (IsDisposed)
            throw new ObjectDisposedException(nameof(GpuRenderPass));
        if (buffer.IsDisposed)
            throw new ObjectDisposedException(nameof(buffer));

        SDL.GPUBufferBinding binding = new SDL.GPUBufferBinding
        {
            Buffer = buffer.BufferId,
            Offset = offset
        };

        SDL.BindGPUIndexBuffer(_gpuRenderPassID, binding, indexElementSize);
    }

    /// <summary>
    /// Sets the viewport for draw calls in this render pass.
    /// </summary>
    public void SetViewport(SDL.GPUViewport viewport)
    {
        if (IsDisposed)
            throw new ObjectDisposedException(nameof(GpuRenderPass));

        SDL.SetGPUViewport(_gpuRenderPassID, viewport);
    }

    /// <summary>
    /// Sets the scissor rectangle for draw calls in this render pass.
    /// </summary>
    public void SetScissor(SDL.Rect scissor)
    {
        if (IsDisposed)
            throw new ObjectDisposedException(nameof(GpuRenderPass));

        SDL.SetGPUScissor(_gpuRenderPassID, scissor);
    }

    /// <summary>
    /// Pushes vertex uniform data for subsequent draw calls in this command buffer.
    /// </summary>
    public void PushVertexUniformData<T>(uint slotIndex, in T data)
        where T : unmanaged
    {
        if (IsDisposed)
            throw new ObjectDisposedException(nameof(GpuRenderPass));

        unsafe
        {
            fixed (T* pData = &data)
            {
                SDL.PushGPUVertexUniformData(_gpuCommandBuffer.CommandBufferId, slotIndex, (nint)pData, (uint)sizeof(T));
            }
        }
    }

    /// <summary>Pushes raw vertex uniform bytes for the given slot.</summary>
    public void PushVertexUniformData(uint slotIndex, ReadOnlySpan<byte> data)
    {
        if (IsDisposed)
            throw new ObjectDisposedException(nameof(GpuRenderPass));
        if (data.IsEmpty) return;

        unsafe
        {
            fixed (byte* pData = data)
            {
                SDL.PushGPUVertexUniformData(_gpuCommandBuffer.CommandBufferId, slotIndex, (nint)pData, (uint)data.Length);
            }
        }
    }

    /// <summary>
    /// Draws non-indexed primitives using the currently bound graphics state.
    /// </summary>
    public void DrawPrimitives(uint numVertices, uint numInstances = 1, uint firstVertex = 0, uint firstInstance = 0)
    {
        if (IsDisposed)
            throw new ObjectDisposedException(nameof(GpuRenderPass));

        SDL.DrawGPUPrimitives(_gpuRenderPassID, numVertices, numInstances, firstVertex, firstInstance);
    }

    /// <summary>
    /// Draws indexed primitives using the currently bound graphics state.
    /// </summary>
    public void DrawIndexedPrimitives(uint numIndices, uint numInstances = 1, uint firstIndex = 0, short vertexOffset = 0, uint firstInstance = 0)
    {
        if (IsDisposed)
            throw new ObjectDisposedException(nameof(GpuRenderPass));

        SDL.DrawGPUIndexedPrimitives(_gpuRenderPassID, numIndices, numInstances, firstIndex, vertexOffset, firstInstance);
    }

    /// <summary>
    /// Pushes fragment uniform data for subsequent draw calls in this command buffer.
    /// </summary>
    public void PushFragmentUniformData<T>(uint slotIndex, in T data)
        where T : unmanaged
    {
        if (IsDisposed)
            throw new ObjectDisposedException(nameof(GpuRenderPass));

        unsafe
        {
            fixed (T* pData = &data)
            {
                SDL.PushGPUFragmentUniformData(_gpuCommandBuffer.CommandBufferId, slotIndex, (nint)pData, (uint)sizeof(T));
            }
        }
    }

    /// <summary>Pushes raw fragment uniform bytes for the given slot.</summary>
    public void PushFragmentUniformData(uint slotIndex, ReadOnlySpan<byte> data)
    {
        if (IsDisposed)
            throw new ObjectDisposedException(nameof(GpuRenderPass));
        if (data.IsEmpty) return;

        unsafe
        {
            fixed (byte* pData = data)
            {
                SDL.PushGPUFragmentUniformData(_gpuCommandBuffer.CommandBufferId, slotIndex, (nint)pData, (uint)data.Length);
            }
        }
    }

    /// <summary>
    /// Binds texture+sampler pairs to the fragment stage starting at the given slot.
    /// </summary>
    public void BindFragmentSamplers(uint firstSlot, ReadOnlySpan<GpuTextureSamplerBinding> bindings)
    {
        if (IsDisposed)
            throw new ObjectDisposedException(nameof(GpuRenderPass));

        unsafe
        {
            Span<SDL.GPUTextureSamplerBinding> native = stackalloc SDL.GPUTextureSamplerBinding[bindings.Length];
            for (int i = 0; i < bindings.Length; i++)
            {
                var b = bindings[i];
                if (b.Texture.IsDisposed)
                    throw new ObjectDisposedException(nameof(b.Texture));
                if (b.Sampler.IsDisposed)
                    throw new ObjectDisposedException(nameof(b.Sampler));
                native[i] = new SDL.GPUTextureSamplerBinding
                {
                    Texture = b.Texture.TextureId,
                    Sampler = b.Sampler.SamplerId,
                };
            }

            fixed (SDL.GPUTextureSamplerBinding* pNative = native)
            {
                SDL.BindGPUFragmentSamplers(_gpuRenderPassID, firstSlot, (nint)pNative, (uint)native.Length);
            }
        }
    }

    /// <summary>
    /// Binds texture+sampler pairs to the vertex stage starting at the given slot.
    /// </summary>
    public void BindVertexSamplers(uint firstSlot, ReadOnlySpan<GpuTextureSamplerBinding> bindings)
    {
        if (IsDisposed)
            throw new ObjectDisposedException(nameof(GpuRenderPass));

        unsafe
        {
            Span<SDL.GPUTextureSamplerBinding> native = stackalloc SDL.GPUTextureSamplerBinding[bindings.Length];
            for (int i = 0; i < bindings.Length; i++)
            {
                var b = bindings[i];
                if (b.Texture.IsDisposed)
                    throw new ObjectDisposedException(nameof(b.Texture));
                if (b.Sampler.IsDisposed)
                    throw new ObjectDisposedException(nameof(b.Sampler));
                native[i] = new SDL.GPUTextureSamplerBinding
                {
                    Texture = b.Texture.TextureId,
                    Sampler = b.Sampler.SamplerId,
                };
            }

            fixed (SDL.GPUTextureSamplerBinding* pNative = native)
            {
                SDL.BindGPUVertexSamplers(_gpuRenderPassID, firstSlot, (nint)pNative, (uint)native.Length);
            }
        }
    }

    /// <summary>
    /// Binds storage buffers to the fragment stage starting at the given slot.
    /// The shader sees them as <c>StructuredBuffer&lt;T&gt;</c> bindings.
    /// </summary>
    public void BindFragmentStorageBuffers(uint firstSlot, ReadOnlySpan<GpuStorageBuffer> buffers)
    {
        if (IsDisposed)
            throw new ObjectDisposedException(nameof(GpuRenderPass));

        unsafe
        {
            Span<nint> ids = stackalloc nint[buffers.Length];
            for (int i = 0; i < buffers.Length; i++)
            {
                var b = buffers[i];
                if (b.IsDisposed)
                    throw new ObjectDisposedException(nameof(b));
                ids[i] = b.BufferId;
            }

            fixed (nint* pIds = ids)
            {
                SDL.BindGPUFragmentStorageBuffers(_gpuRenderPassID, firstSlot, (nint)pIds, (uint)ids.Length);
            }
        }
    }

    /// <summary>
    /// Sets the stencil reference value for subsequent draw calls in this render pass.
    /// </summary>
    public void SetStencilReference(byte reference)
    {
        if (IsDisposed)
            throw new ObjectDisposedException(nameof(GpuRenderPass));

        // SDL3-CS exposes SDL_SetGPUStencilReference as an overload of
        // SetGPUBlendConstants taking a byte (binding quirk).
        SDL.SetGPUBlendConstants(_gpuRenderPassID, reference);
    }

    /// <summary>
    /// Sets the blend constants used by the BlendConstant blend factor.
    /// </summary>
    public void SetBlendConstants(SDL.FColor blendConstants)
    {
        if (IsDisposed)
            throw new ObjectDisposedException(nameof(GpuRenderPass));

        SDL.SetGPUBlendConstants(_gpuRenderPassID, blendConstants);
    }
}

/// <summary>
/// A pairing of a texture and a sampler for binding to a render pass.
/// </summary>
internal readonly record struct GpuTextureSamplerBinding(GpuTexture Texture, GpuSampler Sampler);



/// <summary>
/// Describes one color target used by a render pass.
/// </summary>
internal record GpuColorTargetInfo
{
    // GPUColorTargetInfo

    /// <summary>
    /// The texture that will be used as a color target by a render pass.
    /// </summary>
    public GpuTexture? Texture { get; init; } = default!;

    /// <summary>
    /// The mip level to use as a color target.
    /// </summary>
    public UInt32 MipLevel { get; init; }

    /// <summary>
    /// The layer index or depth plane to use as a color target. This value is treated as a layer index on 2D array and cube textures, and as a depth plane on 3D textures.
    /// </summary>
    public UInt32 LayerOrDepthPlane { get; init; }

    /// <summary>
    /// The color to clear the color target to at the start of the render pass. Ignored if public GPU_LOADOP_CLEAR is not used.
    /// </summary>
    public FColor ClearColor { get; init; }

    /// <summary>
    /// What is done with the contents of the color target at the beginning of the render pass.
    /// </summary>
    public GPULoadOp LoadOp { get; init; } 

    /// <summary>
    /// What is done with the results of the render pass.
    /// </summary>
    public GPUStoreOp StoreOp { get; init; }

    /// <summary>
    /// The texture that will receive the results of a multisample resolve operation. Ignored if a RESOLVE* store_op is not used.
    /// </summary>
    public GpuTexture? ResolveTexture { get; init; } = default!;

    /// <summary>
    /// The mip level of the resolve texture to use for the resolve operation. Ignored if a RESOLVE* store_op is not used.
    /// </summary>
    public UInt32 ResolveMipLevel { get; init; }

    /// <summary>
    /// The layer index of the resolve texture to use for the resolve operation. Ignored if a RESOLVE* store_op is not used.
    /// </summary>
    public UInt32 ResolveLayer { get; init; }

    /// <summary>
    /// true cycles the texture if the texture is bound and load_op is not LOAD
    /// </summary>
    public bool Cycle { get; init; } 

    /// <summary>
    /// true cycles the resolve texture if the resolve texture is bound. Ignored if a RESOLVE* store_op is not used.
    /// </summary>
    public bool CycleResolveTexture { get; init; }
}

/// <summary>
/// Describes the depth and stencil target used by a render pass.
/// </summary>
internal record GpuDepthStencilTargetInfo
{
    // GPUDepthStencilTargetInfo

    /// <summary>
    /// The texture that will be used as the depth stencil target by the render pass.
    /// </summary>
    public GpuTexture? Texture { get; init; }

    /// <summary>
    /// The value to clear the depth component to at the beginning of the render pass. Ignored if public GPU_LOADOP_CLEAR is not used.
    /// </summary>
    public float ClearDepth { get; init; }

    /// <summary>
    /// What is done with the depth contents at the beginning of the render pass.
    /// </summary>
    public GPULoadOp LoadOp { get; init; }

    /// <summary>
    /// What is done with the depth results of the render pass.
    /// </summary>
    public GPUStoreOp StoreOp { get; init; }

    /// <summary>
    /// What is done with the stencil contents at the beginning of the render pass.
    /// </summary>
    public GPULoadOp StencilLoadOp { get; init; }

    /// <summary>
    /// What is done with the stencil results of the render pass.
    /// </summary>
    public GPUStoreOp StencilStoreOp { get; init; }

    /// <summary>
    /// true cycles the texture if the texture is bound and any load ops are not LOAD 
    /// </summary>
    public bool Cycle { get; init; }

    /// <summary>
    /// The value to clear the stencil component to at the beginning of the render pass. Ignored if public GPU_LOADOP_CLEAR is not used.
    /// </summary>
    public Byte ClearStencil { get; init; }
}

/// <summary>
/// A compiled graphics pipeline ready to bind for drawing.
/// </summary>
internal sealed class GpuPipeline : IDisposable
{
    private readonly GpuDevice _gpuDevice;
    private nint _gpuPipelineID;

    internal GpuPipeline(GpuDevice device, nint gpuPipelineID)
    {
        _gpuDevice = device;
        _gpuPipelineID = gpuPipelineID;
        device.AddResource(this);
    }

    internal nint PipelineId => _gpuPipelineID;

    public bool IsDisposed => _gpuPipelineID == 0;

    public void Dispose()
    {
        var id = Interlocked.Exchange(ref _gpuPipelineID, 0);
        if (id != 0)
        {
            SDL.ReleaseGPUGraphicsPipeline(_gpuDevice.GpuDeviceID, id);
            _gpuDevice.RemoveResource(this);
        }
    }

    internal static GpuPipeline CreateGraphicsPipeline(GpuDevice device, GpuPipelineCreateInfo info)
    {
        unsafe
        {
            fixed(SDL.GPUVertexBufferDescription* pBufferDescriptions = info.VertexInputState.BufferDescriptions.AsSpan())
            fixed(SDL.GPUVertexAttribute* pAttributes = info.VertexInputState.Attributes.AsSpan())
            fixed(SDL.GPUColorTargetDescription* pColorTargets = info.TargetInfo.ColorTargetDescriptions.AsSpan())
            {
                var inputState = new SDL.GPUVertexInputState
                {
                    VertexBufferDescriptions = (nint)pBufferDescriptions,
                    NumVertexBuffers = (uint)info.VertexInputState.BufferDescriptions.Length,
                    VertexAttributes = (nint)pAttributes,
                    NumVertexAttributes = (uint)info.VertexInputState.Attributes.Length
                };

                var targetInfo = new SDL.GPUGraphicsPipelineTargetInfo
                {
                    ColorTargetDescriptions = (nint)pColorTargets,
                    NumColorTargets = (uint)info.TargetInfo.ColorTargetDescriptions.Length,
                    DepthStencilFormat = info.TargetInfo.DepthStencilFormat,
                    HasDepthStencilTarget = info.TargetInfo.HasDepthStencilTarget ? (byte)1 : (byte)0,
                };

                var createInfo = new GPUGraphicsPipelineCreateInfo
                {
                    VertexShader = info.VertexShader?.ShaderId ?? 0,
                    FragmentShader = info.FragmentShader?.ShaderId ?? 0,
                    VertexInputState = inputState,
                    PrimitiveType = info.PrimitiveType,
                    RasterizerState = info.RasterizerState,
                    MultisampleState = info.MultisampleState,
                    DepthStencilState = info.DepthStencilState,
                    TargetInfo = targetInfo,
                    Props = info.Properties?.PropertiesId ?? 0
                };

                var pipelineId = SDL.CreateGPUGraphicsPipeline(device.GpuDeviceID, createInfo);
                return new GpuPipeline(device, pipelineId);
            }
        }
    }
}

/// <summary>
/// Describes how to create a graphics pipeline.
/// </summary>
internal record GpuPipelineCreateInfo
{
    // GPUGraphicsPipelineCreateInfo

    /// <summary>
    /// The vertex shader used by the graphics pipeline.
    /// </summary>
    public GpuShader? VertexShader { get; set; }

    /// <summary>
    /// The fragment shader used by the graphics pipeline.
    /// </summary>
    public GpuShader? FragmentShader { get; set; }

    /// <summary>
    /// The vertex layout of the graphics pipeline.
    /// </summary>
    public GpuVertexInputState VertexInputState { get; set; } = default!;

    /// <summary>
    /// The primitive topology of the graphics pipeline.
    /// </summary>
    public GPUPrimitiveType PrimitiveType { get; set; }

    /// <summary>
    /// The rasterizer state of the graphics pipeline.
    /// </summary>
    public GPURasterizerState RasterizerState { get; set; }

    /// <summary>
    /// The multisample state of the graphics pipeline.
    /// </summary>
    public GPUMultisampleState MultisampleState { get; set; }

    /// <summary>
    /// The depth-stencil state of the graphics pipeline.
    /// </summary>
    public GPUDepthStencilState DepthStencilState { get; set; }

    /// <summary>
    /// Formats and blend modes for the render targets of the graphics pipeline.
    /// </summary>
    public GpuPipelineTargetInfo TargetInfo { get; set; } = new GpuPipelineTargetInfo
    {
        ColorTargetDescriptions = ImmutableArray<SDL.GPUColorTargetDescription>.Empty,
    };

    /// <summary>
    /// Properties for extensions. Should be null if no extensions are needed.
    /// </summary>
    public Properties? Properties { get; set; }
}

/// <summary>
/// Describes the vertex buffers and attributes used by a pipeline.
/// </summary>
internal record GpuVertexInputState
{
    // SDL.GPUVertexInputState

    public ImmutableArray<SDL.GPUVertexBufferDescription> BufferDescriptions { get; init; }
    public ImmutableArray<SDL.GPUVertexAttribute> Attributes { get; init; }
}

/// <summary>
/// Describes the target formats and blending state for a pipeline.
/// </summary>
internal record GpuPipelineTargetInfo
{
    // GPUGraphicsPipelineTargetInfo

    /// <summary>
    /// A pointer to an array of color target descriptions.
    /// </summary>
    public ImmutableArray<SDL.GPUColorTargetDescription> ColorTargetDescriptions { get; init; }

    /// <summary>
    /// The pixel format of the depth-stencil target. Ignored if has_depth_stencil_target is false.
    /// </summary>
    public GPUTextureFormat DepthStencilFormat { get; init; }

    /// <summary>
    /// true specifies that the pipeline uses a depth-stencil target.
    /// </summary>
    public bool HasDepthStencilTarget { get; init; }
}

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

/// <summary>
/// Describes how to create a GPU shader module.
/// </summary>
internal record GpuShaderCreateInfo
{
    // GPUShaderCreateInfo

    /// <summary>
    /// The code bytes of the shader.
    /// </summary>
    public ImmutableArray<byte> Code { get; init; }

    /// <summary>
    /// A string specifying the entry point function name for the shader.
    /// </summary>
    public string Entrypoint { get; init; } = "";

    /// <summary>
    /// The format of the shader code.
    /// </summary>
    public GPUShaderFormat Format { get; init; }

    /// <summary>
    /// The stage the shader program corresponds to.
    /// </summary>
    public GPUShaderStage Stage { get; init; }

    /// <summary>
    /// The number of samplers defined in the shader.
    /// </summary>
    public UInt32 NumSamplers { get; init; }

    /// <summary>
    /// The number of storage textures defined in the shader.
    /// </summary>
    public UInt32 NumStorageTextures { get; init; }

    /// <summary>
    /// The number of storage buffers defined in the shader.
    /// </summary>
    public UInt32 NumStorageBuffers { get; init; }

    /// <summary>
    /// The number of uniform buffers defined in the shader.
    /// </summary>
    public UInt32 NumUniformBuffers { get; init; }

    /// <summary>
    /// A properties ID for extensions. Should be 0 if no extensions are needed.
    /// </summary>
    public Properties? Properties { get; init; }
}
