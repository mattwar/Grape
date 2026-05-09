namespace Blitter;

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
