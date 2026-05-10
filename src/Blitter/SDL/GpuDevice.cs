using System.Collections.Immutable;
using Blitter.Utilities;
using static SDL3.SDL;

namespace Blitter;

/// <summary>
/// Corresponds to the GPU hardware device.
/// </summary>
internal sealed class GpuDevice : IDisposable
{
    private ImmutableList<IDisposable> _resources = ImmutableList<IDisposable>.Empty;

    private nint _gpuDeviceID;

    // Per-frame wrapper pools. Owning the pools on the device means
    // every per-frame begin/end of a command buffer, render frame,
    // copy pass, render pass, or fence reuses an existing wrapper
    // instance instead of allocating a fresh one. The wrapper's
    // Dispose returns it to the pool here.
    private readonly Pool<GpuCommandBuffer> _commandBufferPool;
    private readonly Pool<GpuRenderFrame> _renderFramePool;
    private readonly Pool<GpuCopyPass> _copyPassPool;
    private readonly Pool<GpuRenderPass> _renderPassPool;
    private readonly Pool<GpuFence> _fencePool;

    internal GpuDevice(nint gpuDeviceID)
    {
        _gpuDeviceID = gpuDeviceID;
        _commandBufferPool = new Pool<GpuCommandBuffer>(p => new GpuCommandBuffer(p));
        _renderFramePool = new Pool<GpuRenderFrame>(p => new GpuRenderFrame(this, p));
        _copyPassPool = new Pool<GpuCopyPass>(p => new GpuCopyPass(p));
        _renderPassPool = new Pool<GpuRenderPass>(p => new GpuRenderPass(p));
        _fencePool = new Pool<GpuFence>(p => new GpuFence(this, p));
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
    /// Acquires a fresh GPU command buffer (rented from the pool).
    /// </summary>
    public GpuCommandBuffer CreateCommandBuffer() =>
        AllocateCommandBuffer();

    /// <summary>
    /// Begins a frame of GPU work.
    /// </summary>
    public GpuRenderFrame BeginFrame()
    {
        var cb = AllocateCommandBuffer();
        var frame = _renderFramePool.Allocate();
        frame.Init(cb);
        return frame;
    }

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
    /// Create a new graphics pipeline for the GPU.
    /// </summary>
    public GpuPipeline CreateGraphicsPipeline(GpuPipelineCreateInfo info) =>
        GpuPipeline.CreateGraphicsPipeline(this, info);

    // --- Pool-backed allocation of per-frame wrappers ----------------

    internal GpuCommandBuffer AllocateCommandBuffer()
    {
        var commandBufferId = SDL.AcquireGPUCommandBuffer(_gpuDeviceID);
        if (commandBufferId == 0)
            throw new InvalidOperationException(
                $"Failed to acquire GPU command buffer: {SDL.GetError()}");
        var cb = _commandBufferPool.Allocate();
        cb.Init(commandBufferId);
        return cb;
    }

    internal GpuFence AllocateFence(nint fenceId)
    {
        var fence = _fencePool.Allocate();
        fence.Init(fenceId);
        return fence;
    }

    internal GpuCopyPass AllocateCopyPass(GpuCommandBuffer commandBuffer)
    {
        if (!TryAllocateCopyPass(commandBuffer, out var copyPass) || copyPass is null)
            throw new InvalidOperationException(
                $"Failed to begin GPU copy pass: {SDL.GetError()}");
        return copyPass;
    }

    internal bool TryAllocateCopyPass(GpuCommandBuffer commandBuffer, out GpuCopyPass? copyPass)
    {
        var copyPassId = SDL.BeginGPUCopyPass(commandBuffer.CommandBufferId);
        if (copyPassId == 0)
        {
            copyPass = null;
            return false;
        }

        var pass = _copyPassPool.Allocate();
        pass.Init(copyPassId);
        copyPass = pass;
        return true;
    }

    internal GpuRenderPass AllocateRenderPass(
        GpuCommandBuffer commandBuffer,
        ReadOnlySpan<GpuColorTargetInfo> colorTargets,
        GpuDepthStencilTargetInfo depthTarget)
    {
        if (!TryAllocateRenderPass(commandBuffer, colorTargets, depthTarget, out var renderPass)
            || renderPass is null)
            throw new InvalidOperationException(
                $"Failed to begin GPU render pass: {SDL.GetError()}");
        return renderPass;
    }

    internal bool TryAllocateRenderPass(
        GpuCommandBuffer commandBuffer,
        ReadOnlySpan<GpuColorTargetInfo> colorTargets,
        GpuDepthStencilTargetInfo depthTarget,
        out GpuRenderPass? renderPass)
    {
        var renderPassId = BeginRenderPassNative(commandBuffer, colorTargets, depthTarget);
        if (renderPassId == 0)
        {
            renderPass = null;
            return false;
        }

        var pass = _renderPassPool.Allocate();
        pass.Init(commandBuffer, renderPassId);
        renderPass = pass;
        return true;
    }

    private static nint BeginRenderPassNative(
        GpuCommandBuffer commandBuffer,
        ReadOnlySpan<GpuColorTargetInfo> colorTargets,
        GpuDepthStencilTargetInfo depthTarget)
    {
        // Stack-allocate the SDL color-target marshalling buffer; SDL
        // consumes it in BeginGPURenderPass and does not retain it.
        Span<GPUColorTargetInfo> nativeColorTargets = stackalloc GPUColorTargetInfo[colorTargets.Length];
        for (int i = 0; i < colorTargets.Length; i++)
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
                CycleResolveTexture = (byte)(ct.CycleResolveTexture ? 1 : 0),
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
            ClearStencil = depthTarget.ClearStencil,
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
}
