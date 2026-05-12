namespace Blitter.Bits;

/// <summary>
/// Thrown when a <see cref="Materializer"/> can't render the given
/// <see cref="Mesh"/>/<see cref="Material"/> combination -- typically
/// because the material kind isn't recognised, or no shader is
/// registered for the material's vertex format. For instanced draws,
/// also covers the case where the per-instance struct type isn't
/// supported for that material.
/// </summary>
public sealed class MaterializerNotSupportedException : Exception
{
    public MaterializerNotSupportedException(Mesh mesh, Material material)
        : base(BuildMessage(mesh, material, instanceType: null))
    {
        Mesh = mesh;
        Material = material;
    }

    public MaterializerNotSupportedException(Mesh mesh, Material material, Type instanceType)
        : base(BuildMessage(mesh, material, instanceType))
    {
        ArgumentNullException.ThrowIfNull(instanceType);
        Mesh = mesh;
        Material = material;
        InstanceType = instanceType;
    }

    /// <summary>The mesh that couldn't be rendered.</summary>
    public Mesh Mesh { get; }

    /// <summary>The material that couldn't be rendered.</summary>
    public Material Material { get; }

    /// <summary>
    /// The per-instance struct type, when the failure was on an
    /// instanced draw; <c>null</c> for non-instanced failures.
    /// </summary>
    public Type? InstanceType { get; }

    private static string BuildMessage(Mesh mesh, Material material, Type? instanceType)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        ArgumentNullException.ThrowIfNull(material);
        return instanceType is null
            ? $"Materializer has no shader for material '{material.GetType().Name}' " +
              $"with vertex type '{mesh.VertexType.Name}'."
            : $"Materializer has no instanced shader for material '{material.GetType().Name}' " +
              $"with vertex type '{mesh.VertexType.Name}' and instance type '{instanceType.Name}'.";
    }
}
