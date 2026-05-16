using System.Numerics;
using SharpGLTF.Schema2;

namespace Blitter.Bits;

/// <summary>
/// glTF 2.0 / glb model loader. Internal entry point sits behind
/// <see cref="Model.Load(string)"/>. Maps glTF geometry and the
/// metallic-roughness material model into Blitter's
/// <see cref="LitTextureVertex3D"/> + <see cref="PbrMaterial"/> +
/// <see cref="ModelPart"/> types: base-color factor + texture,
/// metallic + roughness factors + packed MR texture, emissive factor +
/// texture, and occlusion strength + texture. Animations, skinning,
/// morph targets, normal maps, tangents, and KHR_materials_unlit are
/// not yet consumed.
/// </summary>
internal static class GLTF
{
    public static Model Load(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        if (!File.Exists(path))
            throw new FileNotFoundException("glTF / glb file not found.", path);

        var root = ModelRoot.Load(path);

        // Cache: glTF Image -> Blitter Image, so two materials sharing a
        // texture share the GPU upload too.
        var imageCache = new Dictionary<SharpGLTF.Schema2.Image, Texture2D>();
        // Same for materials.
        var materialCache = new Dictionary<SharpGLTF.Schema2.Material, PbrMaterial>();

        var parts = new List<ModelPart>();

        // Walk the default scene's node hierarchy. Each node carries a
        // resolved WorldMatrix; we bake that into vertex positions and
        // normals so the resulting flat part list draws correctly
        // without a runtime scene graph. Nodes outside the default
        // scene are skipped (typical glTF practice -- they're often
        // helper nodes like cameras / unused variants).
        var scene = root.DefaultScene ?? root.LogicalScenes.FirstOrDefault();
        if (scene is null)
            return new Model(parts);

        foreach (var node in scene.VisualChildren)
            VisitNode(node, parts, imageCache, materialCache);

        return new Model(parts);
    }

    private static void VisitNode(
        Node node,
        List<ModelPart> parts,
        Dictionary<SharpGLTF.Schema2.Image, Texture2D> imageCache,
        Dictionary<SharpGLTF.Schema2.Material, PbrMaterial> materialCache)
    {
        if (node.Mesh is { } mesh)
        {
            var world = node.WorldMatrix;
            var normalMatrix = ComputeNormalMatrix(world);

            foreach (var prim in mesh.Primitives)
            {
                // We only render triangles. Other primitive types
                // (POINTS, LINES, TRIANGLE_STRIP/FAN) are valid glTF
                // but rare in static assets; skip rather than guess
                // how to triangulate them.
                if (prim.DrawPrimitiveType != PrimitiveType.TRIANGLES)
                    continue;

                var part = BuildPart(prim, world, normalMatrix, imageCache, materialCache, node.Name);
                if (part is not null)
                    parts.Add(part);
            }
        }

        foreach (var child in node.VisualChildren)
            VisitNode(child, parts, imageCache, materialCache);
    }

    private static ModelPart? BuildPart(
        MeshPrimitive prim,
        Matrix4x4 world,
        Matrix4x4 normalMatrix,
        Dictionary<SharpGLTF.Schema2.Image, Texture2D> imageCache,
        Dictionary<SharpGLTF.Schema2.Material, PbrMaterial> materialCache,
        string? nodeName)
    {
        var positions = prim.GetVertexAccessor("POSITION")?.AsVector3Array();
        if (positions is null || positions.Count == 0)
            return null;

        var normals = prim.GetVertexAccessor("NORMAL")?.AsVector3Array();
        var uvs = prim.GetVertexAccessor("TEXCOORD_0")?.AsVector2Array();
        var colors = prim.GetVertexAccessor("COLOR_0")?.AsColorArray();

        var material = ConvertMaterial(prim.Material, materialCache, imageCache);
        // Per-vertex tint carries only the optional COLOR_0 attribute;
        // the PBR shader multiplies it with baseColorTex * baseColorFactor,
        // so baking the material's base-color factor into the vertex
        // here would double-tint. White when COLOR_0 is absent.
        var verts = new LitTextureVertex3D[positions.Count];
        for (int i = 0; i < positions.Count; i++)
        {
            var pos = Vector3.Transform(positions[i], world);
            var nrm = normals is not null
                ? Vector3.Normalize(Vector3.TransformNormal(normals[i], normalMatrix))
                : Vector3.UnitY; // fallback if absent; will be regenerated below if we can.
            var uv = uvs is not null ? uvs[i] : Vector2.Zero;
            var tint = colors is not null ? FloatToColor(colors[i]) : Color.White;

            verts[i] = new LitTextureVertex3D(pos, nrm, uv, tint);
        }

        // Indices: glTF gives us IList<uint>; copy out. If absent, the
        // primitive is non-indexed -- emit 0..N-1 so our renderer's
        // path stays uniform.
        var indexAccessor = prim.IndexAccessor;
        uint[] indices;
        if (indexAccessor is not null)
        {
            var src = indexAccessor.AsIndicesArray();
            indices = new uint[src.Count];
            for (int i = 0; i < src.Count; i++) indices[i] = src[i];
        }
        else
        {
            indices = new uint[positions.Count];
            for (int i = 0; i < indices.Length; i++) indices[i] = (uint)i;
        }

        // If normals were missing, fabricate flat per-face normals and
        // copy them back to the (now post-transformed) vertices. We do
        // this after the world-bake so the normals are already in world
        // space -- no second transform needed.
        if (normals is null)
            FabricateFlatNormals(verts, indices);

        // glTF V coordinate has origin at top of texture, same as
        // SDL_GPU. Unlike OBJ we do NOT flip V on import.

        var mesh = Mesh.Create<LitTextureVertex3D>(verts, indices);
        return new ModelPart(mesh, material, nodeName);
    }

    private static PbrMaterial ConvertMaterial(
        SharpGLTF.Schema2.Material? src,
        Dictionary<SharpGLTF.Schema2.Material, PbrMaterial> materialCache,
        Dictionary<SharpGLTF.Schema2.Image, Texture2D> imageCache)
    {
        if (src is null)
            return PbrMaterial.Default;

        if (materialCache.TryGetValue(src, out var cached))
            return cached;

        // pbrMetallicRoughness: baseColorFactor + baseColorTexture.
        var baseColor = Color.White;
        Texture2D? baseColorTex = null;
        if (src.FindChannel("BaseColor") is { } bc)
        {
            baseColor = FloatToColor(bc.Color);
            if (bc.Texture?.PrimaryImage is { } img)
                baseColorTex = GetOrDecodeImage(img, imageCache);
        }

        // pbrMetallicRoughness: metallicFactor (default 1) and
        // roughnessFactor (default 1) packed into a single channel
        // alongside the (B=metallic, G=roughness) MR texture.
        float metallic = 1f, roughness = 1f;
        Texture2D? mrTex = null;
        if (src.FindChannel("MetallicRoughness") is { } mr)
        {
            metallic = GetFloatParameter(mr, "MetallicFactor", 1f);
            roughness = GetFloatParameter(mr, "RoughnessFactor", 1f);
            if (mr.Texture?.PrimaryImage is { } img)
                mrTex = GetOrDecodeImage(img, imageCache);
        }

        // emissiveFactor is a Vector3 (no alpha); SharpGLTF surfaces it
        // as Vector4 with W=1, so we drop W when converting to Color.
        var emissive = Color.Black;
        Texture2D? emissiveTex = null;
        if (src.FindChannel("Emissive") is { } em)
        {
            var v = em.Color;
            emissive = new Color(
                FloatToByte(v.X), FloatToByte(v.Y), FloatToByte(v.Z), 255);
            if (em.Texture?.PrimaryImage is { } img)
                emissiveTex = GetOrDecodeImage(img, imageCache);
        }

        float occlusionStrength = 1f;
        Texture2D? occlusionTex = null;
        if (src.FindChannel("Occlusion") is { } occ)
        {
            occlusionStrength = GetFloatParameter(occ, "OcclusionStrength", 1f);
            if (occ.Texture?.PrimaryImage is { } img)
                occlusionTex = GetOrDecodeImage(img, imageCache);
        }

        var mat = new PbrMaterial
        {
            BaseColor = baseColor,
            BaseColorTexture = baseColorTex,
            Metallic = metallic,
            Roughness = roughness,
            MetallicRoughnessTexture = mrTex,
            Emissive = emissive,
            EmissiveTexture = emissiveTex,
            OcclusionStrength = occlusionStrength,
            OcclusionTexture = occlusionTex,
            Name = src.Name,
        };
        materialCache[src] = mat;
        return mat;
    }

    private static float GetFloatParameter(
        SharpGLTF.Schema2.MaterialChannel ch, string name, float defaultValue)
    {
        // SharpGLTF exposes per-channel scalars (MetallicFactor,
        // RoughnessFactor, OcclusionStrength, ...) via the Parameters
        // list. Values are boxed as float; absent parameters fall back
        // to the glTF spec default supplied by the caller.
        foreach (var p in ch.Parameters)
            if (p.Name == name && p.Value is float f)
                return f;
        return defaultValue;
    }

    private static Texture2D GetOrDecodeImage(
        SharpGLTF.Schema2.Image gltfImage,
        Dictionary<SharpGLTF.Schema2.Image, Texture2D> imageCache)
    {
        if (imageCache.TryGetValue(gltfImage, out var cached))
            return cached;

        var bytes = gltfImage.Content.Content; // ReadOnlyMemory<byte>; PNG/JPG/etc.
        var img = Bitmap.Decode(bytes.Span, mipmaps: true);
        imageCache[gltfImage] = img;
        return img;
    }

    private static void FabricateFlatNormals(LitTextureVertex3D[] verts, uint[] indices)
    {
        // Per-face flat normals: each triangle (indices i0,i1,i2) gets
        // the same normal written to all three corner vertices. If
        // multiple triangles share a vertex (because we're indexed)
        // the last write wins. For loaders that omit normals this is
        // a deliberately simple fallback -- glTF authors are expected
        // to ship NORMAL.
        for (int t = 0; t < indices.Length; t += 3)
        {
            uint i0 = indices[t + 0];
            uint i1 = indices[t + 1];
            uint i2 = indices[t + 2];
            var p0 = verts[i0].Position;
            var p1 = verts[i1].Position;
            var p2 = verts[i2].Position;
            var n = Vector3.Normalize(Vector3.Cross(p1 - p0, p2 - p0));
            // LitTextureVertex3D is readonly, so we re-emit the struct
            // with the new normal.
            verts[i0] = new LitTextureVertex3D(p0, n, verts[i0].TextureCoordinate, verts[i0].Color);
            verts[i1] = new LitTextureVertex3D(p1, n, verts[i1].TextureCoordinate, verts[i1].Color);
            verts[i2] = new LitTextureVertex3D(p2, n, verts[i2].TextureCoordinate, verts[i2].Color);
        }
    }

    private static Matrix4x4 ComputeNormalMatrix(Matrix4x4 world)
    {
        // Inverse-transpose for non-uniform-scale safety; identity when
        // the matrix isn't invertible (degenerate node, shouldn't
        // happen for real assets).
        if (!Matrix4x4.Invert(world, out var inv))
            return Matrix4x4.Identity;
        return Matrix4x4.Transpose(inv);
    }

    private static Color FloatToColor(Vector4 v) =>
        new(FloatToByte(v.X), FloatToByte(v.Y), FloatToByte(v.Z), FloatToByte(v.W));

    private static byte FloatToByte(float f) =>
        // glTF colors are linear-space floats 0..1. Blitter's Color is
        // sRGB byte. We pass the float values through directly --
        // Blitter's pipeline doesn't currently do gamma correction, so
        // matching the OBJ loader's behavior is more important than
        // strictly-correct color management.
        (byte)Math.Clamp((int)MathF.Round(f * 255f), 0, 255);
}
