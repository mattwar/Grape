namespace Blitter;

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
