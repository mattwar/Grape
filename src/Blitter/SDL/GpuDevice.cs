using System.Collections.Immutable;
using static SDL3.SDL;

namespace Blitter;

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
