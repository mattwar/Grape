namespace Blitter;

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
