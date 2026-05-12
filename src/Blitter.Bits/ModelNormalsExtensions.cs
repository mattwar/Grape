namespace Blitter.Bits;

/// <summary>
/// Model authoring helpers that compute per-vertex normals.
/// </summary>
public static class ModelNormalsExtensions
{
    /// <inheritdoc cref="MeshNormalsExtensions.RecalculateNormals(Mesh{LitTextureVertex3D}, bool)"/>
    public static Model RecalculateNormals(this Model model, bool smooth = true)
    {
        ArgumentNullException.ThrowIfNull(model);
        var newSubs = new Submesh[model.Submeshes.Count];
        for (int i = 0; i < newSubs.Length; i++)
        {
            var s = model.Submeshes[i];
            newSubs[i] = new Submesh(((Mesh<LitTextureVertex3D>)s.Mesh).RecalculateNormals(smooth), s.Material, s.Name);
        }
        return new Model(newSubs, model.SourcePath);
    }
}
