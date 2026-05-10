using System.Numerics;

namespace Blitter.Bits;

/// <summary>
/// Bounds helpers for <see cref="Model"/>. Each call walks every submesh's
/// vertex data; nothing is cached on the model. Store the result yourself
/// (e.g. next to your model reference) if you need bounds repeatedly.
/// </summary>
public static class ModelBounds
{
    /// <summary>
    /// AABB that encloses every submesh of <paramref name="model"/> in
    /// model-local space (no transform applied).
    /// </summary>
    public static BoundingBox ComputeBoundingBox(this Model model)
    {
        ArgumentNullException.ThrowIfNull(model);
        var box = BoundingBox.Empty;
        foreach (var sub in model.Submeshes)
            box = box.Encapsulate(sub.Mesh.ComputeBoundingBox());
        return box;
    }

    /// <summary>
    /// AABB that encloses <paramref name="model"/> after
    /// <paramref name="transform"/> is applied. Each submesh's local AABB
    /// is transformed individually before the union, which is tighter
    /// than transforming a single union AABB once.
    /// </summary>
    public static BoundingBox ComputeBoundingBox(this Model model, Matrix4x4 transform)
    {
        ArgumentNullException.ThrowIfNull(model);
        var box = BoundingBox.Empty;
        foreach (var sub in model.Submeshes)
            box = box.Encapsulate(sub.Mesh.ComputeBoundingBox().Transform(transform));
        return box;
    }

    /// <summary>
    /// Bounding sphere over every vertex in every submesh, in model-local
    /// space. Walks each vertex once (no intermediate array).
    /// </summary>
    public static BoundingSphere ComputeBoundingSphere(this Model model)
    {
        ArgumentNullException.ThrowIfNull(model);

        // Two-pass centroid + farthest-point, computed across all submeshes
        // without copying anything out.
        long total = 0;
        var sum = Vector3.Zero;
        foreach (var sub in model.Submeshes)
        {
            var verts = sub.Mesh.Vertices;
            for (int i = 0; i < verts.Length; i++)
                sum += verts[i].Position;
            total += verts.Length;
        }
        if (total == 0) return BoundingSphere.Empty;
        var center = sum / total;

        float maxSq = 0f;
        foreach (var sub in model.Submeshes)
        {
            var verts = sub.Mesh.Vertices;
            for (int i = 0; i < verts.Length; i++)
            {
                float d = Vector3.DistanceSquared(center, verts[i].Position);
                if (d > maxSq) maxSq = d;
            }
        }
        return new BoundingSphere(center, MathF.Sqrt(maxSq));
    }

    /// <summary>The AABB-center of <paramref name="model"/> in model-local space.</summary>
    public static Vector3 ComputeCenter(this Model model) => model.ComputeBoundingBox().Center;
}
