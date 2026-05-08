using static SDL3.SDL;

namespace Grape;

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
