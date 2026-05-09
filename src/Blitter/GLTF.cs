using System.Numerics;
using SharpGLTF.Schema2;

namespace Blitter;

/// <summary>
/// glTF 2.0 / glb model loader. Internal entry point sits behind
/// <see cref="Model.Load(string)"/>. v1 maps glTF geometry, base-color
/// factor, and base-color texture into Blitter's
/// <see cref="LitTextureVertex3D"/> + <see cref="Material"/> +
/// <see cref="Submesh"/> types. Animations, skinning, morph targets,
/// metallic/roughness/normal/AO maps, and tangents are not yet
/// consumed -- the loader extracts the diffuse channel only and
/// renders Lambert.
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
        var imageCache = new Dictionary<SharpGLTF.Schema2.Image, Image>();
        // Same for materials.
        var materialCache = new Dictionary<SharpGLTF.Schema2.Material, Material>();

        var submeshes = new List<Submesh>();

        // Walk the default scene's node hierarchy. Each node carries a
        // resolved WorldMatrix; we bake that into vertex positions and
        // normals so the resulting flat submesh list draws correctly
        // without a runtime scene graph. Nodes outside the default
        // scene are skipped (typical glTF practice -- they're often
        // helper nodes like cameras / unused variants).
        var scene = root.DefaultScene ?? root.LogicalScenes.FirstOrDefault();
        if (scene is null)
            return new Model(submeshes, path);

        foreach (var node in scene.VisualChildren)
            VisitNode(node, submeshes, imageCache, materialCache);

        return new Model(submeshes, path);
    }

    private static void VisitNode(
        Node node,
        List<Submesh> submeshes,
        Dictionary<SharpGLTF.Schema2.Image, Image> imageCache,
        Dictionary<SharpGLTF.Schema2.Material, Material> materialCache)
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

                var submesh = BuildSubmesh(prim, world, normalMatrix, imageCache, materialCache, node.Name);
                if (submesh is not null)
                    submeshes.Add(submesh);
            }
        }

        foreach (var child in node.VisualChildren)
            VisitNode(child, submeshes, imageCache, materialCache);
    }

    private static Submesh? BuildSubmesh(
        MeshPrimitive prim,
        Matrix4x4 world,
        Matrix4x4 normalMatrix,
        Dictionary<SharpGLTF.Schema2.Image, Image> imageCache,
        Dictionary<SharpGLTF.Schema2.Material, Material> materialCache,
        string? nodeName)
    {
        var positions = prim.GetVertexAccessor("POSITION")?.AsVector3Array();
        if (positions is null || positions.Count == 0)
            return null;

        var normals = prim.GetVertexAccessor("NORMAL")?.AsVector3Array();
        var uvs = prim.GetVertexAccessor("TEXCOORD_0")?.AsVector2Array();
        var colors = prim.GetVertexAccessor("COLOR_0")?.AsColorArray();

        var material = ConvertMaterial(prim.Material, materialCache, imageCache);
        // Bake the material's base-color factor into the vertex tint so
        // the LitTexture shader (sample × tint) reproduces the glTF
        // intended color even when no per-vertex COLOR_0 is supplied.
        // If COLOR_0 *is* present, multiply the two -- glTF spec
        // says they compose multiplicatively.
        var baseTint = material.DiffuseColor;

        var verts = new LitTextureVertex3D[positions.Count];
        for (int i = 0; i < positions.Count; i++)
        {
            var pos = Vector3.Transform(positions[i], world);
            var nrm = normals is not null
                ? Vector3.Normalize(Vector3.TransformNormal(normals[i], normalMatrix))
                : Vector3.UnitY; // fallback if absent; will be regenerated below if we can.
            var uv = uvs is not null ? uvs[i] : Vector2.Zero;
            var tint = colors is not null
                ? MultiplyColors(baseTint, FloatToColor(colors[i]))
                : baseTint;

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
        return new Submesh(mesh, material, nodeName);
    }

    private static Material ConvertMaterial(
        SharpGLTF.Schema2.Material? src,
        Dictionary<SharpGLTF.Schema2.Material, Material> materialCache,
        Dictionary<SharpGLTF.Schema2.Image, Image> imageCache)
    {
        if (src is null)
            return Material.Default;

        if (materialCache.TryGetValue(src, out var cached))
            return cached;

        // The "BaseColor" channel covers both pbrMetallicRoughness's
        // baseColorFactor + baseColorTexture and the legacy KHR_unlit
        // path. We grab whichever is set.
        var color = Color.White;
        Image? texture = null;

        var baseChannel = src.FindChannel("BaseColor");
        if (baseChannel is { } ch)
        {
            color = FloatToColor(ch.Color);

            var srcImage = ch.Texture?.PrimaryImage;
            if (srcImage is not null)
                texture = GetOrDecodeImage(srcImage, imageCache);
        }

        var mat = new Material
        {
            DiffuseColor = color,
            DiffuseTexture = texture,
            Name = src.Name,
        };
        materialCache[src] = mat;
        return mat;
    }

    private static Image GetOrDecodeImage(
        SharpGLTF.Schema2.Image gltfImage,
        Dictionary<SharpGLTF.Schema2.Image, Image> imageCache)
    {
        if (imageCache.TryGetValue(gltfImage, out var cached))
            return cached;

        var bytes = gltfImage.Content.Content; // ReadOnlyMemory<byte>; PNG/JPG/etc.
        var img = Image.Decode(bytes.Span, mipmaps: true);
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

    private static Color FloatToColor(Vector4 v)
    {
        // glTF colors are linear-space floats 0..1. Blitter's Color is
        // sRGB byte. We pass the float values through directly --
        // Blitter's pipeline doesn't currently do gamma correction, so
        // matching the OBJ loader's behavior is more important than
        // strictly-correct color management.
        byte ToByte(float f) => (byte)Math.Clamp((int)MathF.Round(f * 255f), 0, 255);
        return new Color(ToByte(v.X), ToByte(v.Y), ToByte(v.Z), ToByte(v.W));
    }

    private static Color MultiplyColors(Color a, Color b)
    {
        // Per-channel modulate, 0..255 byte range.
        byte M(byte x, byte y) => (byte)((x * y + 127) / 255);
        return new Color(M(a.R, b.R), M(a.G, b.G), M(a.B, b.B), M(a.A, b.A));
    }
}
