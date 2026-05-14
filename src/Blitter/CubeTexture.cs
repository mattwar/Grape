namespace Blitter;

/// <summary>
/// Base type for any cubemap-shaped texture asset (six square faces
/// sampled by a 3D direction vector). Concrete subtypes are
/// <see cref="Cubemap"/> (CPU-backed face images, uploaded to the GPU
/// lazily by the renderer) and <see cref="GpuCubemap"/> (a GPU-only
/// cubemap allocated as a render target).
/// </summary>
/// <remarks>
/// Engine slots that bind a cubemap to a shader (skybox, environment
/// maps, etc.) are typed as <c>CubeTexture</c> so they accept
/// either kind. Code that needs CPU-side face access uses
/// <see cref="Cubemap"/> directly.
/// </remarks>
public abstract class CubeTexture : Texture
{
    /// <summary>
    /// Edge length of every face's base mip level, in pixels. All six
    /// faces are square and share this size.
    /// </summary>
    public abstract int Size { get; }

    /// <summary>Pixel format shared by all six faces.</summary>
    public abstract PixelFormat Format { get; }

    /// <summary>
    /// Number of mip levels stored per face. <c>1</c> means only a
    /// base level exists.
    /// </summary>
    public abstract int LevelCount { get; }

    /// <summary>
    /// Hints to renderers that a mip chain should be auto-generated
    /// for this cubemap on upload. Ignored when <see cref="LevelCount"/>
    /// is already greater than 1.
    /// </summary>
    public abstract bool Mipmaps { get; }

    /// <summary>
    /// Bumped each time the cubemap's contents change. Renderers use
    /// this to detect when their cached GPU upload is stale.
    /// </summary>
    public abstract int Version { get; }

    /// <summary>
    /// Marks the cubemap contents as changed so renderers re-upload
    /// on the next draw.
    /// </summary>
    public abstract void Invalidate();
}
