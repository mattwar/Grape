namespace Grape;

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
