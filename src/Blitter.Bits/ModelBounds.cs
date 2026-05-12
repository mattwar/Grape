using System.Numerics;

namespace Blitter.Bits;

/// <summary>
/// Helpers to compute bounds for a <see cref="Model"/>.
/// </summary>
public static class ModelBounds
{
    /// <summary>
    /// The bounding box that encloses every part of the <paramref name="model"/>.
    /// </summary>
    public static BoundingBox ComputeBoundingBox(this Model model)
    {
        ArgumentNullException.ThrowIfNull(model);
        var box = BoundingBox.Empty;
        foreach (var sub in model.Parts)
            box = box.Encapsulate(((Mesh<LitTextureVertex3D>)sub.Mesh).ComputeBoundingBox());
        return box;
    }

    /// <summary>
    /// The bounding box that encloses the transformed bounding boxes of each part of the <paramref name="model"/>.
    /// </summary>
    public static BoundingBox ComputeBoundingBox(this Model model, Matrix4x4 transform)
    {
        ArgumentNullException.ThrowIfNull(model);
        var box = BoundingBox.Empty;
        foreach (var sub in model.Parts)
            box = box.Encapsulate(((Mesh<LitTextureVertex3D>)sub.Mesh).ComputeBoundingBox().Transform(transform));
        return box;
    }

    /// <summary>
    /// The bounding sphere that encloses every part of the <paramref name="model"/>.
    /// </summary>
    public static BoundingSphere ComputeBoundingSphere(this Model model)
    {
        ArgumentNullException.ThrowIfNull(model);

        // Two-pass centroid + farthest-point, computed across all parts
        // without copying anything out.
        long total = 0;
        var sum = Vector3.Zero;
        foreach (var sub in model.Parts)
        {
            var verts = ((Mesh<LitTextureVertex3D>)sub.Mesh).Vertices;
            for (int i = 0; i < verts.Length; i++)
                sum += verts[i].Position;
            total += verts.Length;
        }
        if (total == 0) return BoundingSphere.Empty;
        var center = sum / total;

        float maxSq = 0f;
        foreach (var sub in model.Parts)
        {
            var verts = ((Mesh<LitTextureVertex3D>)sub.Mesh).Vertices;
            for (int i = 0; i < verts.Length; i++)
            {
                float d = Vector3.DistanceSquared(center, verts[i].Position);
                if (d > maxSq) maxSq = d;
            }
        }
        return new BoundingSphere(center, MathF.Sqrt(maxSq));
    }

    /// <summary>
    /// The center of the bounding box of the <paramref name="model"/>.
    /// </summary>
    public static Vector3 ComputeCenter(this Model model) => 
        model.ComputeBoundingBox().Center;

    /// <summary>
    /// The bounding boxes for each part of <paramref name="model"/>.
    /// </summary>
    public static BoundingBox[] ComputeBoundingBoxes(this Model model, Matrix4x4 transform)
    {
        ArgumentNullException.ThrowIfNull(model);
        var boxes = new BoundingBox[model.Parts.Length];
        for (int i = 0; i < boxes.Length; i++)
            boxes[i] = ((Mesh<LitTextureVertex3D>)model.Parts[i].Mesh).ComputeBoundingBox().Transform(transform);
        return boxes;
    }

    /// <summary>
    /// The bounding boxes for each part of <paramref name="model"/>.
    /// </summary>
    public static BoundingBox[] ComputeBoundingBoxes(this Model model)
    {
        ArgumentNullException.ThrowIfNull(model);
        var boxes = new BoundingBox[model.Parts.Length];
        for (int i = 0; i < boxes.Length; i++)
            boxes[i] = ((Mesh<LitTextureVertex3D>)model.Parts[i].Mesh).ComputeBoundingBox();
        return boxes;
    }

    /// <summary>
    /// The occupied bounding boxes for each part of <paramref name="model"/>.
    /// </summary>
    public static BoundingBox[] ComputeOccupiedBoxes(
        this Model model,
        float voxelSize,
        MeshOccupancyMode mode = MeshOccupancyMode.Accurate)
    {
        ArgumentNullException.ThrowIfNull(model);
        return MeshOccupancy.ComputeForModel(model, voxelSize, mode);
    }
}
