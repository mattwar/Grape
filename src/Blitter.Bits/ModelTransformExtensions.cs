using System.Numerics;

namespace Blitter.Bits;

/// <summary>
/// Simple transformations of model parts. 
/// For per-frame alterations use a transform at draw time instead.
/// </summary>
public static class ModelTransformExtensions
{
    /// <summary>
    /// Returns a new model whose part vertices have been transformed by <paramref name="matrix"/>.
    /// </summary>
    public static Model Transform(this Model model, Matrix4x4 matrix)
    {
        ArgumentNullException.ThrowIfNull(model);
        var newSubs = new ModelPart[model.Parts.Length];
        for (int i = 0; i < newSubs.Length; i++)
        {
            var s = model.Parts[i];
            newSubs[i] = new ModelPart(((Mesh<LitTextureVertex3D>)s.Mesh).Transform(matrix), s.Material, s.Name);
        }
        return new Model(newSubs);
    }

    public static Model Translate(this Model model, Vector3 offset) =>
        model.Transform(Matrix4x4.CreateTranslation(offset));

    public static Model Translate(this Model model, float x, float y, float z) =>
        model.Transform(Matrix4x4.CreateTranslation(x, y, z));

    public static Model Scale(this Model model, float scale) =>
        model.Transform(Matrix4x4.CreateScale(scale));

    public static Model Scale(this Model model, Vector3 scale) =>
        model.Transform(Matrix4x4.CreateScale(scale));

    public static Model Rotate(this Model model, Quaternion rotation) =>
        model.Transform(Matrix4x4.CreateFromQuaternion(rotation));

    public static Model RotateX(this Model model, float radians) =>
        model.Transform(Matrix4x4.CreateRotationX(radians));

    public static Model RotateY(this Model model, float radians) =>
        model.Transform(Matrix4x4.CreateRotationY(radians));

    public static Model RotateZ(this Model model, float radians) =>
        model.Transform(Matrix4x4.CreateRotationZ(radians));

    /// <summary>
    /// Returns a new model translated so its aggregate bounding box is centered at the origin
    /// </summary>
    public static Model CenterOnOrigin(this Model model)
    {
        ArgumentNullException.ThrowIfNull(model);
        var box = model.ComputeBoundingBox();
        if (box.IsEmpty) return model;
        return model.Translate(-box.Center);
    }

    /// <summary>
    /// Returns a new model uniformly scaled so its longest bounding-box dimension equals <paramref name="targetMaxSize"/>. 
    /// Centering is not changed; chain with <see cref="CenterOnOrigin"/> if you want both.
    /// </summary>
    public static Model NormalizeSize(this Model model, float targetMaxSize = 1f)
    {
        ArgumentNullException.ThrowIfNull(model);
        if (targetMaxSize <= 0f)
            throw new ArgumentOutOfRangeException(nameof(targetMaxSize),
                "Target size must be positive.");

        var box = model.ComputeBoundingBox();
        if (box.IsEmpty) return model;
        var size = box.Size;
        var maxDim = MathF.Max(size.X, MathF.Max(size.Y, size.Z));
        if (maxDim <= 0f) return model;
        return model.Scale(targetMaxSize / maxDim);
    }
}
