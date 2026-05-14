namespace Blitter;

/// <summary>
/// A 3D renderer that draws into a single (face, mip) of a
/// <see cref="GpuCubemap"/>. Used by GPU cubemap bakers (irradiance,
/// prefiltered specular) that issue one fullscreen pass per face/mip.
/// </summary>
internal sealed class GpuCubemapFaceRenderer : GpuRenderer
{
    private readonly GpuCubemap _target;
    private readonly CubeFace _face;
    private readonly int _mip;
    private readonly SDL.GPUTextureFormat _format;
    private readonly uint _faceSizeAtMip;
    private readonly bool _useDepth;

    internal GpuCubemapFaceRenderer(GpuDevice device, GpuCubemap target, CubeFace face, int mip, bool useDepth = true)
        : base(device)
    {
        ArgumentNullException.ThrowIfNull(target);
        if ((uint)mip >= (uint)target.LevelCount)
            throw new ArgumentOutOfRangeException(nameof(mip));

        _target = target;
        _face = face;
        _mip = mip;
        _format = GpuPixelFormatMap.ToGpu(target.Format);
        _faceSizeAtMip = (uint)Math.Max(1, target.Size >> mip);
        _useDepth = useDepth;
    }

    /// <summary>The cubemap this renderer is drawing into.</summary>
    internal GpuCubemap Target => _target;

    /// <summary>The face currently bound as the color target.</summary>
    internal CubeFace Face => _face;

    /// <summary>The mip level currently bound as the color target.</summary>
    internal int Mip => _mip;

    // Bake passes are fullscreen-triangle postprocesses with no
    // overlapping geometry; depth would be wasted bandwidth and force
    // a per-mip depth scratch allocation. General scene renders into a
    // face (public GpuCubemap.Render) keep depth enabled.
    protected override bool UsesDepthBuffer => _useDepth;

    protected override bool TryAcquireColorTarget(
        GpuRenderFrame frame,
        out GpuTexture? colorTarget,
        out SDL.GPUTextureFormat colorFormat,
        out uint width,
        out uint height,
        out uint layer,
        out uint mipLevel)
    {
        if (_target.IsDisposed)
        {
            colorTarget = null;
            colorFormat = SDL.GPUTextureFormat.Invalid;
            width = 0;
            height = 0;
            layer = 0;
            mipLevel = 0;
            return false;
        }

        colorTarget = _target.Texture;
        colorFormat = _format;
        width = _faceSizeAtMip;
        height = _faceSizeAtMip;
        layer = (uint)(int)_face;
        mipLevel = (uint)_mip;
        return true;
    }
}
