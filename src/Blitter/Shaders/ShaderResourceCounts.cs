namespace Blitter;

/// <summary>
/// Resource counts a shader stage exposes to the pipeline. Must match what
/// the bytecode actually declares; the GPU validator will reject mismatches.
/// </summary>
public readonly record struct ShaderResourceCounts(
    uint NumSamplers = 0,
    uint NumUniformBuffers = 0,
    uint NumStorageTextures = 0,
    uint NumStorageBuffers = 0);
