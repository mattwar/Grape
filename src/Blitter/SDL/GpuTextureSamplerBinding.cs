namespace Blitter;

/// <summary>
/// A pairing of a texture and a sampler for binding to a render pass.
/// </summary>
internal readonly record struct GpuTextureSamplerBinding(GpuTexture Texture, GpuSampler Sampler);
