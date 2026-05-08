namespace Grape;

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
