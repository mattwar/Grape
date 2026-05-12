using System.Collections.Immutable;

namespace Blitter.Bits;

/// <summary>
/// A collection of meshes and materials parts.
/// </summary>
public sealed class Model
{
    /// <summary>
    /// The model's parts, in the order they were declared.
    /// </summary>
    public ImmutableArray<ModelPart> Parts { get; }

    public Model(IEnumerable<ModelPart> parts)
    {
        ArgumentNullException.ThrowIfNull(parts);
        Parts = parts.ToImmutableArray();
    }

    public Model(ImmutableArray<ModelPart> parts)
    {
        // ImmutableArray<T> is a struct, so it's never null -- but
        // `default(ImmutableArray<T>)` is uninitialized (IsDefault),
        // and any access on it throws NullReferenceException.
        // Substitute an empty array so Parts is always usable.
        Parts = parts.IsDefault ? ImmutableArray<ModelPart>.Empty : parts;
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

/// <summary>
/// One drawable piece of a <see cref="Model"/>
/// </summary>
public sealed class ModelPart
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

    public ModelPart(Mesh mesh, Material material, string? name = null)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        ArgumentNullException.ThrowIfNull(material);
        Mesh = mesh;
        Material = material;
        Name = name;
    }
}


