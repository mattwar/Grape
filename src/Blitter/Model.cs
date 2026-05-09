using System.Numerics;

namespace Blitter;

/// <summary>
/// One drawable piece of a <see cref="Model"/>: a mesh paired with the
/// material that should be applied to it. A model with several
/// materials produces several submeshes, one per material, because
/// material switches require separate draw calls.
/// </summary>
/// <remarks>
/// All loader-produced submeshes use <see cref="LitTextureVertex3D"/>
/// (position + normal + uv + color) so a single shader covers every
/// loaded surface. If you want a different vertex format for a
/// hand-built model, build the submesh list directly rather than going
/// through the loader.
/// </remarks>
public sealed class Submesh
{
    /// <summary>The vertex/index data.</summary>
    public Mesh<LitTextureVertex3D> Mesh { get; }

    /// <summary>The material to render this mesh with.</summary>
    public Material Material { get; }

    /// <summary>
    /// Optional human-readable name (e.g. the OBJ group/object name
    /// the faces were under). Useful for debugging.
    /// </summary>
    public string? Name { get; }

    public Submesh(Mesh<LitTextureVertex3D> mesh, Material material, string? name = null)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        ArgumentNullException.ThrowIfNull(material);
        Mesh = mesh;
        Material = material;
        Name = name;
    }
}

/// <summary>
/// A loaded 3D asset -- a list of <see cref="Submesh"/> plus the
/// materials they share. Built by a loader (today
/// <see cref="Model.Load(string)"/> for OBJ files) or assembled
/// directly. Drawing is one call: <see cref="Draw"/> walks every
/// submesh, applies its material, and queues the draw through
/// <see cref="ShaderSets.LitTexture"/>.
/// </summary>
public sealed class Model : IDisposable
{
    private readonly Image _whitePlaceholder;
    private bool _disposed;

    /// <summary>The model's submeshes, in the order they were loaded.</summary>
    public IReadOnlyList<Submesh> Submeshes { get; }

    /// <summary>
    /// Source path the model was loaded from, or <c>null</c> if it was
    /// assembled in code. Useful for diagnostics.
    /// </summary>
    public string? SourcePath { get; }

    public Model(IEnumerable<Submesh> submeshes, string? sourcePath = null)
    {
        ArgumentNullException.ThrowIfNull(submeshes);
        Submeshes = submeshes.ToArray();
        SourcePath = sourcePath;
        _whitePlaceholder = CreateWhitePlaceholder();
    }

    /// <summary>
    /// Loads a 3D model from disk. Today this dispatches by extension:
    /// <c>.obj</c> goes through the OBJ loader (any <c>.mtl</c>
    /// sidecar referenced by <c>mtllib</c> is loaded too) and
    /// <c>.glb</c> / <c>.gltf</c> go through the glTF loader. Future
    /// formats register here.
    /// </summary>
    public static Model Load(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        var ext = Path.GetExtension(path);
        if (ext.Equals(".obj", StringComparison.OrdinalIgnoreCase))
            return OBJ.Load(path);
        if (ext.Equals(".glb", StringComparison.OrdinalIgnoreCase) ||
            ext.Equals(".gltf", StringComparison.OrdinalIgnoreCase))
            return GLTF.Load(path);
        throw new NotSupportedException(
            $"Unsupported model format '{ext}'. Supported: .obj, .glb, .gltf.");
    }

    /// <summary>
    /// Queues every submesh for drawing on <paramref name="renderer"/>,
    /// transformed by <paramref name="transform"/>. The renderer's
    /// camera, ambient, directional, and point lights are composed in
    /// automatically through <see cref="IUniformArgs{TSelf}"/>.
    /// </summary>
    public void Draw(Renderer3D renderer, Matrix4x4 transform)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        ThrowIfDisposed();

        foreach (var sub in Submeshes)
        {
            var args = new LitArgs(transform);

            // Bake the material's diffuse color into the args via the
            // vertex tint... no, the tint lives on the vertex itself
            // for OBJ-loaded models (the loader bakes it in there so
            // every submesh's vertices already carry the material's
            // Kd). For hand-assembled submeshes that supplied white
            // vertices and a colored material, the tint is *not*
            // applied -- this is a known limitation worth revisiting
            // when materials grow a per-draw uniform tier.
            //
            // Today's behavior: the texture is what varies per-draw;
            // tint is whatever the vertex carries.
            var texture = sub.Material.DiffuseTexture ?? _whitePlaceholder;
            renderer.DrawMesh(sub.Mesh, texture, ShaderSets.LitTexture, args);
        }
    }

    /// <summary>
    /// Convenience overload: identity transform.
    /// </summary>
    public void Draw(Renderer3D renderer) => Draw(renderer, Matrix4x4.Identity);

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        // Dispose only synthesised resources. The submesh meshes are
        // pure CPU data (no GPU lifetime to manage); the placeholder
        // image was created here, so we own it.
        _whitePlaceholder.Dispose();
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(Model));
    }

    // Stand-in for materials that don't supply a diffuse texture. A
    // single white pixel keeps the LitTexture shader's sampler bind
    // valid (it always expects a Texture2D) without contributing any
    // color of its own -- the shader multiplies the sample by the
    // vertex tint, so a white sample passes the tint through unchanged.
    private static Image CreateWhitePlaceholder()
    {
        var img = Image.Create(1, 1, PixelFormat.ABGR8888);
        img.SetPixel(0, 0, Color.White);
        return img;
    }
}
