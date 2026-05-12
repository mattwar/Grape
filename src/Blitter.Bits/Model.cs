using System.Numerics;

namespace Blitter.Bits;

/// <summary>
/// One drawable piece of a <see cref="Model"/>
/// </summary>
public sealed class Submesh
{
    /// <summary>
    /// The vertex data, index and topology.
    /// </summary>
    public Mesh Mesh { get; }

    /// <summary>The material to render this mesh with.</summary>
    public Material Material { get; }

    /// <summary>
    /// Optional name; useful for debugging.
    /// </summary>
    public string? Name { get; }

    public Submesh(Mesh mesh, Material material, string? name = null)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        ArgumentNullException.ThrowIfNull(material);
        Mesh = mesh;
        Material = material;
        Name = name;
    }
}

/// <summary>
/// A multi-mesh Model.
/// </summary>
public sealed class Model
{
    /// <summary>The model's submeshes, in the order they were loaded.</summary>
    public IReadOnlyList<Submesh> Submeshes { get; }

    /// <summary>
    /// Source path the model was loaded from, or <c>null</c> if it was assembled in code. 
    /// Useful for diagnostics.
    /// </summary>
    public string? SourcePath { get; }

    public Model(IEnumerable<Submesh> submeshes, string? sourcePath = null)
    {
        ArgumentNullException.ThrowIfNull(submeshes);
        Submeshes = submeshes.ToArray();
        SourcePath = sourcePath;
    }

    /// <summary>
    /// Loads a 3D model from disk.
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
}

