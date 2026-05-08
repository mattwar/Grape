using System.Globalization;
using System.Numerics;

namespace Grape;

/// <summary>
/// Wavefront OBJ + MTL loader. Internal entry point sits behind
/// <see cref="Model.Load(string)"/>; expose this directly only if you
/// need to load from a non-file source.
/// </summary>
internal static class OBJ
{
    private static readonly char[] WhitespaceSeparators = { ' ', '\t' };

    // Default key for faces that appear before any group/material is
    // declared. OBJ files often start emitting faces immediately, with
    // no `g`/`o`/`usemtl`, so we need a stable bucket name.
    private const string DefaultGroupName = "";
    private const string DefaultMaterialName = "";

    public static Model Load(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        if (!File.Exists(path))
            throw new FileNotFoundException("OBJ file not found.", path);

        var directory = Path.GetDirectoryName(Path.GetFullPath(path)) ?? string.Empty;

        // Global vertex-attribute pools; faces reference into these by
        // 1-based index (negative = from end).
        var positions = new List<Vector3>();
        var texCoords = new List<Vector2>();
        var normals = new List<Vector3>();

        // Loaded materials keyed by `newmtl` name. The empty-string key
        // covers face groups that never had a `usemtl` -- it points to
        // a default material so the build pass doesn't need a special
        // case.
        var materials = new Dictionary<string, Material>(StringComparer.Ordinal)
        {
            [DefaultMaterialName] = Material.Default,
        };

        // One bucket per (group, material) pair. Face order within a
        // bucket is preserved; bucket creation order drives submesh
        // order in the resulting model.
        var bucketOrder = new List<(string Group, string Material)>();
        var buckets = new Dictionary<(string Group, string Material), List<FaceCorner[]>>();

        var activeGroup = DefaultGroupName;
        var activeMaterial = DefaultMaterialName;

        using (var reader = new StreamReader(path))
        {
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                // Trim and skip empties / comments without splitting --
                // most lines short-circuit here.
                var trimmed = line.AsSpan().Trim();
                if (trimmed.IsEmpty || trimmed[0] == '#')
                    continue;

                var tokens = line.Split(WhitespaceSeparators, StringSplitOptions.RemoveEmptyEntries);
                if (tokens.Length == 0)
                    continue;

                switch (tokens[0])
                {
                    case "v":
                        positions.Add(ParseVector3(tokens));
                        break;

                    case "vt":
                        // OBJ uses bottom-left UV origin; flip V on
                        // import so loaded textures look right with
                        // the top-left convention SDL_GPU + most
                        // image loaders use.
                        var uv = ParseVector2(tokens);
                        texCoords.Add(new Vector2(uv.X, 1f - uv.Y));
                        break;

                    case "vn":
                        normals.Add(ParseVector3(tokens));
                        break;

                    case "g":
                    case "o":
                        // Use the first name token; OBJ allows multiple
                        // (intersection of groups) but we treat them as
                        // a single label.
                        activeGroup = tokens.Length >= 2 ? tokens[1] : DefaultGroupName;
                        break;

                    case "usemtl":
                        activeMaterial = tokens.Length >= 2 ? tokens[1] : DefaultMaterialName;
                        break;

                    case "mtllib":
                        // Each token after `mtllib` is a separate
                        // library file. Resolve relative to the .obj's
                        // directory and merge all materials into one
                        // dictionary; later libraries can replace
                        // earlier definitions of the same name.
                        for (int i = 1; i < tokens.Length; i++)
                        {
                            var mtlPath = ResolveRelative(directory, tokens[i]);
                            if (!File.Exists(mtlPath))
                                continue;
                            foreach (var (name, mat) in MTL.Load(mtlPath))
                                materials[name] = mat;
                        }
                        break;

                    case "f":
                        var face = ParseFace(tokens, positions.Count, texCoords.Count, normals.Count);
                        if (face.Length < 3)
                            break; // ill-formed; skip

                        var key = (activeGroup, activeMaterial);
                        if (!buckets.TryGetValue(key, out var faces))
                        {
                            faces = new List<FaceCorner[]>();
                            buckets[key] = faces;
                            bucketOrder.Add(key);
                        }
                        faces.Add(face);
                        break;

                    // s (smoothing group): we always flat-shade
                    //     fabricated normals, so ignore.
                    // l (line element), p (point element): the renderer
                    //     can do these but the loader's surface model
                    //     is "filled triangle meshes" -- skip.
                    default:
                        break;
                }
            }
        }

        // When the file omits per-vertex normals (`vn`), fabricate a
        // smoothed normal per position by accumulating area-weighted
        // face normals from every face that touches it. This produces
        // the same result as "shade smooth" in a DCC tool: adjacent
        // triangles across a curved surface share interpolated
        // normals and read as continuous instead of faceted. Faces
        // (or individual corners) that *do* carry a `vn` keep their
        // authored normal -- the smooth table is only consulted for
        // corners that lack one. Skipping the pass when every face
        // already supplies normals avoids touching well-authored OBJs.
        var smoothNormals = AnyCornerLacksNormal(buckets)
            ? ComputeSmoothNormals(buckets, positions)
            : null;

        // Build pass: turn each bucket into a deduplicated
        // LitTextureVertex3D mesh + Submesh.
        var submeshes = new List<Submesh>();
        foreach (var key in bucketOrder)
        {
            var faceList = buckets[key];
            if (faceList.Count == 0)
                continue;

            if (!materials.TryGetValue(key.Material, out var material))
                material = Material.Default;

            var (vertices, indices) = BuildSubmesh(faceList, positions, texCoords, normals, smoothNormals, material);
            if (vertices.Length == 0)
                continue;

            var mesh = Mesh.Create<LitTextureVertex3D>(vertices, indices);
            // Submesh name: prefer the group label; fall back to
            // "<material>" so an unnamed-group OBJ still produces
            // distinguishable submeshes.
            var name = !string.IsNullOrEmpty(key.Group) ? key.Group :
                       !string.IsNullOrEmpty(key.Material) ? key.Material :
                       null;
            submeshes.Add(new Submesh(mesh, material, name));
        }

        return new Model(submeshes, path);
    }

    // Triangulate + deduplicate one bucket into a vertex array + index buffer.
    private static (LitTextureVertex3D[] V, uint[] I) BuildSubmesh(
        List<FaceCorner[]> faces,
        List<Vector3> positions,
        List<Vector2> texCoords,
        List<Vector3> normals,
        Vector3[]? smoothNormals,
        Material material)
    {
        // Bake the material's diffuse color into every emitted vertex.
        // The shader multiplies texture sample × vertex color, so this
        // is how a "color only" material reaches the surface and how a
        // "color × texture" material composes -- both for free, no
        // per-draw uniform tier required.
        Color tint = material.DiffuseColor;

        // Dedup key spans both the source indices and the resolved
        // normal vector. Source indices alone aren't enough because
        // when a face uses a fabricated face normal (e.g. mixed mesh
        // where smoothNormals is null but some corners still lack vn),
        // two adjacent faces sharing a position must still produce two
        // distinct vertices for their differing fabricated normals.
        var dedupe = new Dictionary<VertexKey, uint>();
        var verts = new List<LitTextureVertex3D>();
        var indices = new List<uint>();

        foreach (var face in faces)
        {
            // When no smoothed normal table is available, fall back
            // to a per-face cross product so corners without `vn`
            // still get *some* normal. This preserves the historical
            // flat-shaded behavior for files that mix authored and
            // missing normals on a per-face basis.
            Vector3 faceNormal = Vector3.Zero;
            bool needsFaceNormal = smoothNormals is null;
            if (needsFaceNormal)
            {
                bool anyMissing = false;
                for (int i = 0; i < face.Length; i++)
                {
                    if (face[i].Normal < 0) { anyMissing = true; break; }
                }
                if (anyMissing)
                    faceNormal = ComputeFaceNormal(face, positions);
                else
                    needsFaceNormal = false;
            }

            // Fan triangulation: (0, t, t+1). Works for convex
            // polygons, which covers the vast majority of authored
            // OBJs (almost always quads/triangles).
            for (int t = 1; t < face.Length - 1; t++)
            {
                AddCorner(face[0]);
                AddCorner(face[t]);
                AddCorner(face[t + 1]);
            }

            void AddCorner(FaceCorner c)
            {
                if (c.Position < 0 || c.Position >= positions.Count)
                    return; // malformed; drop the triangle silently
                Vector3 pos = positions[c.Position];
                Vector2 uv = c.TexCoord >= 0 && c.TexCoord < texCoords.Count
                    ? texCoords[c.TexCoord]
                    : Vector2.Zero;
                Vector3 n = c.Normal >= 0 && c.Normal < normals.Count
                    ? normals[c.Normal]
                    : (smoothNormals is not null ? smoothNormals[c.Position] : faceNormal);

                var key = new VertexKey(c.Position, c.TexCoord, c.Normal, n);
                if (!dedupe.TryGetValue(key, out var idx))
                {
                    idx = (uint)verts.Count;
                    verts.Add(new LitTextureVertex3D(pos, n, uv, tint));
                    dedupe[key] = idx;
                }
                indices.Add(idx);
            }
        }

        return (verts.ToArray(), indices.ToArray());
    }

    private static Vector3 ComputeFaceNormal(FaceCorner[] face, List<Vector3> positions)
    {
        // Newell's method would be more robust on non-planar faces,
        // but a simple cross product of the first triangle's edges is
        // good enough for the convex polygons OBJ files contain in
        // practice. Falls back to up if the face is degenerate.
        if (face.Length < 3) return Vector3.UnitY;
        if (face[0].Position < 0 || face[1].Position < 0 || face[2].Position < 0)
            return Vector3.UnitY;
        var a = positions[face[0].Position];
        var b = positions[face[1].Position];
        var c = positions[face[2].Position];
        var n = Vector3.Cross(b - a, c - a);
        var len = n.Length();
        return len > 1e-6f ? n / len : Vector3.UnitY;
    }

    private static bool AnyCornerLacksNormal(
        Dictionary<(string Group, string Material), List<FaceCorner[]>> buckets)
    {
        foreach (var faces in buckets.Values)
            foreach (var face in faces)
                for (int i = 0; i < face.Length; i++)
                    if (face[i].Normal < 0)
                        return true;
        return false;
    }

    // Area-weighted average of incident face normals, indexed by
    // position. The cross product magnitude is twice the triangle's
    // area, so adding the un-normalized cross product per fan
    // triangle weights each contribution by area for free -- larger
    // triangles get proportionally more say in the smoothed normal,
    // which matches what every smoothing-group implementation does.
    private static Vector3[] ComputeSmoothNormals(
        Dictionary<(string Group, string Material), List<FaceCorner[]>> buckets,
        List<Vector3> positions)
    {
        var accum = new Vector3[positions.Count];

        foreach (var faces in buckets.Values)
        {
            foreach (var face in faces)
            {
                if (face.Length < 3) continue;
                int i0 = face[0].Position;
                if (i0 < 0 || i0 >= positions.Count) continue;
                Vector3 a = positions[i0];

                // Fan triangulation matches BuildSubmesh so each
                // emitted triangle contributes its own face normal
                // (and area weight) to the three positions it touches.
                for (int t = 1; t < face.Length - 1; t++)
                {
                    int i1 = face[t].Position;
                    int i2 = face[t + 1].Position;
                    if (i1 < 0 || i1 >= positions.Count) continue;
                    if (i2 < 0 || i2 >= positions.Count) continue;

                    Vector3 b = positions[i1];
                    Vector3 c = positions[i2];
                    Vector3 weighted = Vector3.Cross(b - a, c - a);
                    accum[i0] += weighted;
                    accum[i1] += weighted;
                    accum[i2] += weighted;
                }
            }
        }

        for (int i = 0; i < accum.Length; i++)
        {
            float len = accum[i].Length();
            // Isolated vertices (in no face) get a benign +Y so a
            // stray reference doesn't produce a zero normal that
            // crashes the lighting math.
            accum[i] = len > 1e-6f ? accum[i] / len : Vector3.UnitY;
        }
        return accum;
    }

    private static FaceCorner[] ParseFace(string[] tokens, int posCount, int texCount, int normCount)
    {
        // tokens[0] is "f"; the rest are corners.
        var corners = new FaceCorner[tokens.Length - 1];
        for (int i = 1; i < tokens.Length; i++)
        {
            corners[i - 1] = ParseCorner(tokens[i], posCount, texCount, normCount);
        }
        return corners;
    }

    private static FaceCorner ParseCorner(string token, int posCount, int texCount, int normCount)
    {
        // Forms: "v", "v/vt", "v/vt/vn", "v//vn". Any of vt or vn may
        // be empty (i.e. "v//vn"). All indices are 1-based; negative
        // means "from end".
        int p = -1, t = -1, n = -1;

        // Hand-roll the split to avoid allocating a string[] per corner.
        ReadOnlySpan<char> s = token;
        int slash1 = s.IndexOf('/');
        if (slash1 < 0)
        {
            p = ParseIndex(s, posCount);
        }
        else
        {
            p = ParseIndex(s[..slash1], posCount);
            var rest = s[(slash1 + 1)..];
            int slash2 = rest.IndexOf('/');
            if (slash2 < 0)
            {
                t = ParseIndex(rest, texCount);
            }
            else
            {
                if (slash2 > 0)
                    t = ParseIndex(rest[..slash2], texCount);
                n = ParseIndex(rest[(slash2 + 1)..], normCount);
            }
        }

        return new FaceCorner(p, t, n);
    }

    private static int ParseIndex(ReadOnlySpan<char> span, int count)
    {
        if (span.IsEmpty) return -1;
        if (!int.TryParse(span, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v))
            return -1;
        // Convert 1-based to 0-based; resolve negative indices.
        if (v > 0) return v - 1;
        if (v < 0) return count + v; // -1 means "last", which is count-1.
        return -1; // 0 is invalid in OBJ.
    }

    private static Vector2 ParseVector2(string[] tokens)
    {
        // tokens[0] is the directive ("vt"); coords start at [1].
        float x = tokens.Length > 1 ? ParseFloat(tokens[1]) : 0f;
        float y = tokens.Length > 2 ? ParseFloat(tokens[2]) : 0f;
        return new Vector2(x, y);
    }

    private static Vector3 ParseVector3(string[] tokens)
    {
        float x = tokens.Length > 1 ? ParseFloat(tokens[1]) : 0f;
        float y = tokens.Length > 2 ? ParseFloat(tokens[2]) : 0f;
        float z = tokens.Length > 3 ? ParseFloat(tokens[3]) : 0f;
        return new Vector3(x, y, z);
    }

    private static float ParseFloat(string s) =>
        float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 0f;

    internal static string ResolveRelative(string baseDirectory, string fileToken)
    {
        // OBJ paths sometimes use forward slashes even on Windows.
        // Path.Combine handles either as long as there's no leading
        // separator, but Path.IsPathRooted checks platform-specific.
        if (Path.IsPathRooted(fileToken))
            return fileToken;
        return Path.Combine(baseDirectory, fileToken);
    }

    private readonly record struct FaceCorner(int Position, int TexCoord, int Normal);

    // Dedupe key. Includes the resolved normal vector (not just the
    // index) so fabricated face normals don't collide with real
    // per-vertex normals at the same position.
    private readonly record struct VertexKey(int PosIdx, int TexIdx, int NormIdx, Vector3 ResolvedNormal);
}
