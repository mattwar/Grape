using static SDL3.SDL;

namespace Grape;

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
