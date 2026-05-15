namespace Blitter;

internal static class GpuPixelFormatMap
{
    // Selects the GPU-side texture format corresponding to a CPU
    // PixelFormat for the formats we support as GPU-resident texture
    // contents. Centralized so GpuBitmap, GpuCubemap, and the renderer's
    // upload paths agree.
    internal static SDL.GPUTextureFormat ToGpu(PixelFormat format) => format switch
    {
        PixelFormat.ABGR8888 => SDL.GPUTextureFormat.R8G8B8A8Unorm,
        PixelFormat.RGBA64Float => SDL.GPUTextureFormat.R16G16B16A16Float,
        _ => throw new NotSupportedException($"Pixel format {format} cannot be represented directly as a GPU texture; use ABGR8888 or RGBA64Float."),
    };
}
