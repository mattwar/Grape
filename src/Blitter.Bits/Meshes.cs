using System.Numerics;

namespace Blitter.Bits;

/// <summary>
/// Built-in procedural mesh generators for common 3D primitives.
/// All meshes are centered on the origin. Solids return lit vertex
/// types so they shade correctly under
/// <see cref="Renderer3D.DirectionalLight"/> /
/// <see cref="Renderer3D.AmbientLight"/>; lines and 2D shapes return
/// unlit <see cref="ColorVertex3D"/>.
/// </summary>
public static class Meshes
{
    // ----- 2D / billboards (XY plane, +Z facing, unlit) ----------------

    /// <summary>
    /// Two-triangle rectangle (quad) on the XY plane with a uniform vertex
    /// color. Default size is 1x1; pass a <see cref="Vector2"/> for
    /// non-square rectangles (e.g. aspect-correct billboards or HUD panels).
    /// </summary>
    public static Mesh<ColorVertex3D> Rectangle(Color color, Vector2? size = null)
    {
        var s = (size ?? Vector2.One) * 0.5f;
        return Mesh.Create<ColorVertex3D>(
            [
                new(new Vector3(-s.X, -s.Y, 0f), color),
                new(new Vector3( s.X, -s.Y, 0f), color),
                new(new Vector3( s.X,  s.Y, 0f), color),
                new(new Vector3(-s.X,  s.Y, 0f), color),
            ],
            [0, 1, 2, 0, 2, 3]);
    }

    /// <summary>Square (equal-sided <see cref="Rectangle"/>) on the XY plane.</summary>
    public static Mesh<ColorVertex3D> Square(Color color, float size = 1f)
        => Rectangle(color, new Vector2(size, size));

    /// <summary>
    /// Two-triangle rectangle (quad) on the XY plane with UVs going (0,0)
    /// at the top-left to (1,1) at the bottom-right (matches SDL_GPU's
    /// origin-at-top texture convention). Default size is 1x1; pass a
    /// <see cref="Vector2"/> for non-square rectangles.
    /// </summary>
    public static Mesh<TextureVertex3D> TexturedRectangle(Vector2? size = null)
    {
        var s = (size ?? Vector2.One) * 0.5f;
        return Mesh.Create<TextureVertex3D>(
            [
                new(new Vector3(-s.X, -s.Y, 0f), new Vector2(0f, 1f)),
                new(new Vector3( s.X, -s.Y, 0f), new Vector2(1f, 1f)),
                new(new Vector3( s.X,  s.Y, 0f), new Vector2(1f, 0f)),
                new(new Vector3(-s.X,  s.Y, 0f), new Vector2(0f, 0f)),
            ],
            [0, 1, 2, 0, 2, 3]);
    }

    /// <summary>Square version of <see cref="TexturedRectangle"/>.</summary>
    public static Mesh<TextureVertex3D> TexturedSquare(float size = 1f)
        => TexturedRectangle(new Vector2(size, size));

    /// <summary>
    /// Filled ellipse (oval) on the XY plane. <paramref name="size"/> is
    /// the full width and height (default 1x1, equivalent to a unit
    /// <see cref="Circle"/>).
    /// </summary>
    public static Mesh<ColorVertex3D> Ellipse(Color color, Vector2? size = null, int segments = 32)
    {
        if (segments < 3) throw new ArgumentOutOfRangeException(nameof(segments));

        var s = (size ?? Vector2.One) * 0.5f;
        var verts = new ColorVertex3D[segments + 1];
        verts[0] = new ColorVertex3D(Vector3.Zero, color);
        for (int i = 0; i < segments; i++)
        {
            float a = i * MathF.Tau / segments;
            verts[i + 1] = new ColorVertex3D(
                new Vector3(MathF.Cos(a) * s.X, MathF.Sin(a) * s.Y, 0f), color);
        }

        var indices = new uint[segments * 3];
        for (int i = 0; i < segments; i++)
        {
            indices[i * 3 + 0] = 0;
            indices[i * 3 + 1] = (uint)(i + 1);
            indices[i * 3 + 2] = (uint)((i + 1) % segments + 1);
        }
        return Mesh.Create<ColorVertex3D>(verts, indices);
    }

    /// <summary>Filled circle (disk) on the XY plane — <see cref="Ellipse"/> with equal axes.</summary>
    public static Mesh<ColorVertex3D> Circle(Color color, float radius = 0.5f, int segments = 32)
        => Ellipse(color, new Vector2(radius * 2f, radius * 2f), segments);

    /// <summary>Annular ring on the XY plane.</summary>
    public static Mesh<ColorVertex3D> Ring(
        Color color,
        float innerRadius = 0.4f,
        float outerRadius = 0.5f,
        int segments = 32)
    {
        if (segments < 3) throw new ArgumentOutOfRangeException(nameof(segments));
        if (innerRadius < 0f || outerRadius <= innerRadius)
            throw new ArgumentException("outerRadius must be greater than innerRadius and both non-negative.");

        var verts = new ColorVertex3D[segments * 2];
        for (int i = 0; i < segments; i++)
        {
            float a = i * MathF.Tau / segments;
            float c = MathF.Cos(a), s = MathF.Sin(a);
            verts[i * 2 + 0] = new ColorVertex3D(new Vector3(c * innerRadius, s * innerRadius, 0f), color);
            verts[i * 2 + 1] = new ColorVertex3D(new Vector3(c * outerRadius, s * outerRadius, 0f), color);
        }

        var indices = new uint[segments * 6];
        for (int i = 0; i < segments; i++)
        {
            uint i0 = (uint)(i * 2);            // inner this
            uint i1 = (uint)(i * 2 + 1);        // outer this
            uint i2 = (uint)(((i + 1) % segments) * 2);     // inner next
            uint i3 = (uint)(((i + 1) % segments) * 2 + 1); // outer next
            indices[i * 6 + 0] = i0;
            indices[i * 6 + 1] = i1;
            indices[i * 6 + 2] = i3;
            indices[i * 6 + 3] = i0;
            indices[i * 6 + 4] = i3;
            indices[i * 6 + 5] = i2;
        }
        return Mesh.Create<ColorVertex3D>(verts, indices);
    }

    // ----- 3D solids (lit) ---------------------------------------------

    /// <summary>
    /// Subdivided plane on the XZ plane (normal +Y).
    /// Default size is 1x1 (X by Z); pass a <see cref="Vector2"/> for
    /// non-square planes. <paramref name="subdivisions"/> sets cells per
    /// side. Tessellation matters when point-light positions vary across
    /// the surface; otherwise <c>subdivisions: 1</c> suffices.
    /// </summary>
    public static Mesh<LitVertex3D> Plane(Color color, Vector2? size = null, int subdivisions = 1)
    {
        if (subdivisions < 1) throw new ArgumentOutOfRangeException(nameof(subdivisions));

        var sz = size ?? Vector2.One;
        int n = subdivisions + 1;
        var verts = new LitVertex3D[n * n];
        for (int z = 0; z < n; z++)
        {
            for (int x = 0; x < n; x++)
            {
                float fx = (x / (float)subdivisions - 0.5f) * sz.X;
                float fz = (z / (float)subdivisions - 0.5f) * sz.Y;
                verts[z * n + x] = new LitVertex3D(new Vector3(fx, 0f, fz), Vector3.UnitY, color);
            }
        }
        var indices = new uint[subdivisions * subdivisions * 6];
        int k = 0;
        for (int z = 0; z < subdivisions; z++)
        {
            for (int x = 0; x < subdivisions; x++)
            {
                uint a = (uint)(z * n + x);
                uint b = a + 1;
                uint c = (uint)((z + 1) * n + x);
                uint d = c + 1;
                indices[k++] = a; indices[k++] = c; indices[k++] = b;
                indices[k++] = b; indices[k++] = c; indices[k++] = d;
            }
        }
        return Mesh.Create<LitVertex3D>(verts, indices);
    }

    /// <summary>
    /// Same as <see cref="Plane"/> but with UVs spanning (0,0) at
    /// (-X,-Z) corner to (1,1) at (+X,+Z) corner.
    /// </summary>
    public static Mesh<LitTextureVertex3D> TexturedPlane(
        Vector2? size = null, int subdivisions = 1, Color? tint = null)
    {
        if (subdivisions < 1) throw new ArgumentOutOfRangeException(nameof(subdivisions));

        var c = tint ?? Color.White;
        var sz = size ?? Vector2.One;
        int n = subdivisions + 1;
        var verts = new LitTextureVertex3D[n * n];
        for (int z = 0; z < n; z++)
        {
            for (int x = 0; x < n; x++)
            {
                float u = x / (float)subdivisions;
                float v = z / (float)subdivisions;
                float fx = (u - 0.5f) * sz.X;
                float fz = (v - 0.5f) * sz.Y;
                verts[z * n + x] = new LitTextureVertex3D(
                    new Vector3(fx, 0f, fz), Vector3.UnitY, new Vector2(u, v), c);
            }
        }
        var indices = new uint[subdivisions * subdivisions * 6];
        int k = 0;
        for (int z = 0; z < subdivisions; z++)
        {
            for (int x = 0; x < subdivisions; x++)
            {
                uint a = (uint)(z * n + x);
                uint b = a + 1;
                uint c2 = (uint)((z + 1) * n + x);
                uint d = c2 + 1;
                indices[k++] = a; indices[k++] = c2; indices[k++] = b;
                indices[k++] = b; indices[k++] = c2; indices[k++] = d;
            }
        }
        return Mesh.Create<LitTextureVertex3D>(verts, indices);
    }

    // Cube face data shared by Cube / TexturedCube. Each face has a
    // normal and four corners A,B,C,D wound CCW when viewed from +N.
    private static readonly (Vector3 N, Vector3 A, Vector3 B, Vector3 C, Vector3 D)[] _unitCubeFaces =
    [
        (new( 1, 0, 0), new( 1,-1,-1), new( 1, 1,-1), new( 1, 1, 1), new( 1,-1, 1)), // +X
        (new(-1, 0, 0), new(-1,-1, 1), new(-1, 1, 1), new(-1, 1,-1), new(-1,-1,-1)), // -X
        (new( 0, 1, 0), new(-1, 1, 1), new( 1, 1, 1), new( 1, 1,-1), new(-1, 1,-1)), // +Y
        (new( 0,-1, 0), new(-1,-1,-1), new( 1,-1,-1), new( 1,-1, 1), new(-1,-1, 1)), // -Y
        (new( 0, 0, 1), new(-1,-1, 1), new( 1,-1, 1), new( 1, 1, 1), new(-1, 1, 1)), // +Z
        (new( 0, 0,-1), new( 1,-1,-1), new(-1,-1,-1), new(-1, 1,-1), new( 1, 1,-1)), // -Z
    ];

    /// <summary>
    /// Cube with per-face normals (24 vertices, 36 indices). Default
    /// size is unit-edge (extent 1 on every axis); pass a
    /// <see cref="Vector3"/> for non-cubic boxes.
    /// </summary>
    public static Mesh<LitVertex3D> Cube(Color color, Vector3? size = null)
    {
        var s = (size ?? Vector3.One) * 0.5f;
        var verts = new LitVertex3D[24];
        var indices = new uint[36];
        int vi = 0, ii = 0;
        foreach (var (n, a, b, c, d) in _unitCubeFaces)
        {
            uint b0 = (uint)vi;
            verts[vi++] = new LitVertex3D(a * s, n, color);
            verts[vi++] = new LitVertex3D(b * s, n, color);
            verts[vi++] = new LitVertex3D(c * s, n, color);
            verts[vi++] = new LitVertex3D(d * s, n, color);
            indices[ii++] = b0 + 0; indices[ii++] = b0 + 1; indices[ii++] = b0 + 2;
            indices[ii++] = b0 + 0; indices[ii++] = b0 + 2; indices[ii++] = b0 + 3;
        }
        return Mesh.Create<LitVertex3D>(verts, indices);
    }

    /// <summary>
    /// Cube with per-face normals and per-face UVs (each face spans
    /// (0,0)..(1,1)). 24 vertices, 36 indices.
    /// </summary>
    public static Mesh<LitTextureVertex3D> TexturedCube(Vector3? size = null, Color? tint = null)
    {
        var s = (size ?? Vector3.One) * 0.5f;
        var c = tint ?? Color.White;
        var verts = new LitTextureVertex3D[24];
        var indices = new uint[36];
        int vi = 0, ii = 0;
        foreach (var (n, a, b, c2, d) in _unitCubeFaces)
        {
            uint b0 = (uint)vi;
            verts[vi++] = new LitTextureVertex3D(a * s, n, new Vector2(0f, 1f), c);
            verts[vi++] = new LitTextureVertex3D(b * s, n, new Vector2(1f, 1f), c);
            verts[vi++] = new LitTextureVertex3D(c2 * s, n, new Vector2(1f, 0f), c);
            verts[vi++] = new LitTextureVertex3D(d * s, n, new Vector2(0f, 0f), c);
            indices[ii++] = b0 + 0; indices[ii++] = b0 + 1; indices[ii++] = b0 + 2;
            indices[ii++] = b0 + 0; indices[ii++] = b0 + 2; indices[ii++] = b0 + 3;
        }
        return Mesh.Create<LitTextureVertex3D>(verts, indices);
    }

    /// <summary>
    /// UV sphere (latitude/longitude grid) with smooth normals.
    /// <paramref name="latitudeSegments"/> stacks from pole to pole,
    /// <paramref name="longitudeSegments"/> slices around.
    /// </summary>
    public static Mesh<LitVertex3D> Sphere(
        Color color, float radius = 0.5f, int latitudeSegments = 16, int longitudeSegments = 32)
    {
        if (latitudeSegments < 2) throw new ArgumentOutOfRangeException(nameof(latitudeSegments));
        if (longitudeSegments < 3) throw new ArgumentOutOfRangeException(nameof(longitudeSegments));

        BuildUvSpherePositions(radius, latitudeSegments, longitudeSegments,
            out var positions, out var indices);

        var verts = new LitVertex3D[positions.Length];
        for (int i = 0; i < positions.Length; i++)
        {
            var p = positions[i];
            verts[i] = new LitVertex3D(p, Vector3.Normalize(p), color);
        }
        return Mesh.Create<LitVertex3D>(verts, indices);
    }

    /// <summary>Same as <see cref="Sphere"/> with UVs (longitude, latitude).</summary>
    public static Mesh<LitTextureVertex3D> TexturedSphere(
        float radius = 0.5f, int latitudeSegments = 16, int longitudeSegments = 32, Color? tint = null)
    {
        if (latitudeSegments < 2) throw new ArgumentOutOfRangeException(nameof(latitudeSegments));
        if (longitudeSegments < 3) throw new ArgumentOutOfRangeException(nameof(longitudeSegments));

        var c = tint ?? Color.White;
        BuildUvSpherePositions(radius, latitudeSegments, longitudeSegments,
            out var positions, out var indices);

        // Recompute UVs in lockstep with the position layout.
        var verts = new LitTextureVertex3D[positions.Length];
        int idx = 0;
        for (int lat = 0; lat <= latitudeSegments; lat++)
        {
            float v = lat / (float)latitudeSegments;
            for (int lon = 0; lon <= longitudeSegments; lon++)
            {
                float u = lon / (float)longitudeSegments;
                var p = positions[idx];
                verts[idx] = new LitTextureVertex3D(p, Vector3.Normalize(p), new Vector2(u, v), c);
                idx++;
            }
        }
        return Mesh.Create<LitTextureVertex3D>(verts, indices);
    }

    private static void BuildUvSpherePositions(
        float radius, int latSegs, int lonSegs,
        out Vector3[] positions, out uint[] indices)
    {
        int rows = latSegs + 1;   // pole ring counts on both ends
        int cols = lonSegs + 1;   // duplicate seam vertex for clean UVs
        positions = new Vector3[rows * cols];
        for (int lat = 0; lat <= latSegs; lat++)
        {
            float theta = lat * MathF.PI / latSegs;
            float sinT = MathF.Sin(theta), cosT = MathF.Cos(theta);
            for (int lon = 0; lon <= lonSegs; lon++)
            {
                float phi = lon * MathF.Tau / lonSegs;
                float sinP = MathF.Sin(phi), cosP = MathF.Cos(phi);
                positions[lat * cols + lon] = new Vector3(
                    radius * sinT * cosP,
                    radius * cosT,
                    radius * sinT * sinP);
            }
        }

        var idx = new List<uint>(latSegs * lonSegs * 6);
        for (int lat = 0; lat < latSegs; lat++)
        {
            for (int lon = 0; lon < lonSegs; lon++)
            {
                uint a = (uint)(lat * cols + lon);
                uint b = a + 1;
                uint c = (uint)((lat + 1) * cols + lon);
                uint d = c + 1;
                // CCW from outside (looking along -normal toward origin).
                idx.Add(a); idx.Add(c); idx.Add(b);
                idx.Add(b); idx.Add(c); idx.Add(d);
            }
        }
        indices = idx.ToArray();
    }

    /// <summary>
    /// Geodesic sphere built by subdividing an icosahedron and projecting
    /// midpoints onto the sphere. Triangles are far more uniform than a
    /// UV sphere; recommended for large surfaces lit from many angles.
    /// </summary>
    public static Mesh<LitVertex3D> Icosphere(Color color, float radius = 0.5f, int subdivisions = 2)
    {
        if (subdivisions < 0) throw new ArgumentOutOfRangeException(nameof(subdivisions));

        BuildIcosphere(subdivisions, out var positions, out var indices);

        var verts = new LitVertex3D[positions.Length];
        for (int i = 0; i < positions.Length; i++)
        {
            var n = positions[i]; // already unit length
            verts[i] = new LitVertex3D(n * radius, n, color);
        }
        return Mesh.Create<LitVertex3D>(verts, indices);
    }

    private static void BuildIcosphere(int subdivisions, out Vector3[] positions, out uint[] indices)
    {
        // Initial icosahedron (12 vertices, 20 faces). Vertices are
        // normalized so subdivision math stays on the unit sphere.
        float t = (1f + MathF.Sqrt(5f)) * 0.5f;
        var verts = new List<Vector3>
        {
            Vector3.Normalize(new(-1,  t,  0)),
            Vector3.Normalize(new( 1,  t,  0)),
            Vector3.Normalize(new(-1, -t,  0)),
            Vector3.Normalize(new( 1, -t,  0)),
            Vector3.Normalize(new( 0, -1,  t)),
            Vector3.Normalize(new( 0,  1,  t)),
            Vector3.Normalize(new( 0, -1, -t)),
            Vector3.Normalize(new( 0,  1, -t)),
            Vector3.Normalize(new( t,  0, -1)),
            Vector3.Normalize(new( t,  0,  1)),
            Vector3.Normalize(new(-t,  0, -1)),
            Vector3.Normalize(new(-t,  0,  1)),
        };
        var faces = new List<(int A, int B, int C)>
        {
            (0,11, 5), (0, 5, 1), (0, 1, 7), (0, 7,10), (0,10,11),
            (1, 5, 9), (5,11, 4), (11,10, 2), (10, 7, 6), (7, 1, 8),
            (3, 9, 4), (3, 4, 2), (3, 2, 6), (3, 6, 8), (3, 8, 9),
            (4, 9, 5), (2, 4,11), (6, 2,10), (8, 6, 7), (9, 8, 1),
        };

        var midCache = new Dictionary<long, int>();
        int Midpoint(int a, int b)
        {
            long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
            if (midCache.TryGetValue(key, out int existing)) return existing;
            var m = Vector3.Normalize((verts[a] + verts[b]) * 0.5f);
            int i = verts.Count;
            verts.Add(m);
            midCache[key] = i;
            return i;
        }

        for (int s = 0; s < subdivisions; s++)
        {
            var next = new List<(int A, int B, int C)>(faces.Count * 4);
            foreach (var (a, b, c) in faces)
            {
                int ab = Midpoint(a, b);
                int bc = Midpoint(b, c);
                int ca = Midpoint(c, a);
                next.Add((a, ab, ca));
                next.Add((b, bc, ab));
                next.Add((c, ca, bc));
                next.Add((ab, bc, ca));
            }
            faces = next;
        }

        positions = verts.ToArray();
        indices = new uint[faces.Count * 3];
        int k = 0;
        foreach (var (a, b, c) in faces)
        {
            indices[k++] = (uint)a;
            indices[k++] = (uint)b;
            indices[k++] = (uint)c;
        }
    }

    /// <summary>
    /// Cylinder along the Y axis with smooth side normals. Optional
    /// flat caps at +Y/2 and -Y/2.
    /// </summary>
    public static Mesh<LitVertex3D> Cylinder(
        Color color, float radius = 0.5f, float height = 1f, int segments = 24, bool capped = true)
    {
        if (segments < 3) throw new ArgumentOutOfRangeException(nameof(segments));

        float h = height * 0.5f;
        int sideCols = segments + 1; // duplicate seam for normals/UVs symmetry
        var verts = new List<LitVertex3D>(sideCols * 2 + (capped ? 2 + segments * 2 : 0));
        var indices = new List<uint>();

        // Side: two rings of vertices, smooth radial normals.
        for (int i = 0; i <= segments; i++)
        {
            float a = i * MathF.Tau / segments;
            float cx = MathF.Cos(a), cz = MathF.Sin(a);
            var n = new Vector3(cx, 0f, cz);
            verts.Add(new LitVertex3D(new Vector3(cx * radius, -h, cz * radius), n, color));
            verts.Add(new LitVertex3D(new Vector3(cx * radius,  h, cz * radius), n, color));
        }
        for (int i = 0; i < segments; i++)
        {
            uint b0 = (uint)(i * 2);
            uint b1 = b0 + 1;
            uint b2 = b0 + 2;
            uint b3 = b0 + 3;
            indices.Add(b0); indices.Add(b1); indices.Add(b3);
            indices.Add(b0); indices.Add(b3); indices.Add(b2);
        }

        if (capped)
        {
            // Top cap (normal +Y).
            int topCenter = verts.Count;
            verts.Add(new LitVertex3D(new Vector3(0f, h, 0f), Vector3.UnitY, color));
            int topRingStart = verts.Count;
            for (int i = 0; i < segments; i++)
            {
                float a = i * MathF.Tau / segments;
                verts.Add(new LitVertex3D(
                    new Vector3(MathF.Cos(a) * radius, h, MathF.Sin(a) * radius),
                    Vector3.UnitY, color));
            }
            for (int i = 0; i < segments; i++)
            {
                indices.Add((uint)topCenter);
                indices.Add((uint)(topRingStart + i));
                indices.Add((uint)(topRingStart + (i + 1) % segments));
            }

            // Bottom cap (normal -Y), reversed winding.
            int botCenter = verts.Count;
            verts.Add(new LitVertex3D(new Vector3(0f, -h, 0f), -Vector3.UnitY, color));
            int botRingStart = verts.Count;
            for (int i = 0; i < segments; i++)
            {
                float a = i * MathF.Tau / segments;
                verts.Add(new LitVertex3D(
                    new Vector3(MathF.Cos(a) * radius, -h, MathF.Sin(a) * radius),
                    -Vector3.UnitY, color));
            }
            for (int i = 0; i < segments; i++)
            {
                indices.Add((uint)botCenter);
                indices.Add((uint)(botRingStart + (i + 1) % segments));
                indices.Add((uint)(botRingStart + i));
            }
        }

        return Mesh.Create<LitVertex3D>(verts.ToArray(), indices.ToArray());
    }

    /// <summary>
    /// Cone with apex at +Y/2 and base ring at -Y/2. Smooth side
    /// normals (computed against the slant). Optional base cap.
    /// </summary>
    public static Mesh<LitVertex3D> Cone(
        Color color, float radius = 0.5f, float height = 1f, int segments = 24, bool capped = true)
    {
        if (segments < 3) throw new ArgumentOutOfRangeException(nameof(segments));

        float h = height * 0.5f;
        // Slant normal: at base point (cos a, 0, sin a), the outward normal
        // tilts upward. With apex above and the side surface going from
        // (r cos, -h, r sin) up to (0, h, 0), the outward normal is
        // normalize((cos a * height, radius, sin a * height)).
        var verts = new List<LitVertex3D>();
        var indices = new List<uint>();

        // Per-segment side: 3 verts with the segment's flat-ish normal.
        // We use one normal per segment (averaged from the two edge
        // tangents). Apex is duplicated per segment so the apex normal
        // matches that segment's side -- avoids the singular-pole UV
        // problem and keeps shading consistent.
        for (int i = 0; i < segments; i++)
        {
            float a0 = i * MathF.Tau / segments;
            float a1 = (i + 1) * MathF.Tau / segments;
            var p0 = new Vector3(MathF.Cos(a0) * radius, -h, MathF.Sin(a0) * radius);
            var p1 = new Vector3(MathF.Cos(a1) * radius, -h, MathF.Sin(a1) * radius);
            var apex = new Vector3(0f, h, 0f);
            // Face normal of the triangle (p0, p1, apex), pointing outward.
            var n = Vector3.Normalize(Vector3.Cross(p1 - p0, apex - p0));
            uint b0 = (uint)verts.Count;
            verts.Add(new LitVertex3D(p0, n, color));
            verts.Add(new LitVertex3D(p1, n, color));
            verts.Add(new LitVertex3D(apex, n, color));
            indices.Add(b0); indices.Add(b0 + 2); indices.Add(b0 + 1);
        }

        if (capped)
        {
            int center = verts.Count;
            verts.Add(new LitVertex3D(new Vector3(0f, -h, 0f), -Vector3.UnitY, color));
            int ring = verts.Count;
            for (int i = 0; i < segments; i++)
            {
                float a = i * MathF.Tau / segments;
                verts.Add(new LitVertex3D(
                    new Vector3(MathF.Cos(a) * radius, -h, MathF.Sin(a) * radius),
                    -Vector3.UnitY, color));
            }
            for (int i = 0; i < segments; i++)
            {
                indices.Add((uint)center);
                indices.Add((uint)(ring + (i + 1) % segments));
                indices.Add((uint)(ring + i));
            }
        }

        return Mesh.Create<LitVertex3D>(verts.ToArray(), indices.ToArray());
    }

    /// <summary>
    /// Capsule along the Y axis: a cylinder of length
    /// <paramref name="height"/> with hemispherical caps of
    /// <paramref name="radius"/> on each end. Total Y extent is
    /// <c>height + 2 * radius</c>.
    /// </summary>
    public static Mesh<LitVertex3D> Capsule(
        Color color, float radius = 0.25f, float height = 0.5f, int segments = 24, int hemisphereRings = 8)
    {
        if (segments < 3) throw new ArgumentOutOfRangeException(nameof(segments));
        if (hemisphereRings < 1) throw new ArgumentOutOfRangeException(nameof(hemisphereRings));

        float hh = height * 0.5f;
        int cols = segments + 1; // duplicate seam vertex for clean wrapping
        // Two hemispheres of (hemisphereRings + 1) rows each. Both equator
        // rows are emitted -- the top equator sits at +hh, the bottom
        // equator at -hh, and the quad strip between them forms the
        // cylinder body. Total ring rows = 2 * (hemisphereRings + 1).
        int rows = 2 * (hemisphereRings + 1);
        var verts = new LitVertex3D[rows * cols];

        for (int r = 0; r < rows; r++)
        {
            // hr in [0..hemisphereRings]
            //   top hemi:    0 = top pole,    hemisphereRings = top equator
            //   bottom hemi: 0 = bot equator, hemisphereRings = bottom pole
            bool topHemi = r <= hemisphereRings;
            int hr = topHemi ? r : r - (hemisphereRings + 1);
            float theta = hr * (MathF.PI * 0.5f) / hemisphereRings;
            float sinT = MathF.Sin(theta);
            float cosT = MathF.Cos(theta);

            // Radial scale for x/z, y component of the unit normal, and the
            // vertical center offset of this hemisphere differ by hemisphere.
            float radial = topHemi ? sinT : cosT;
            float ny     = topHemi ? cosT : -sinT;
            float yCenter = topHemi ? hh : -hh;

            for (int c = 0; c <= segments; c++)
            {
                float phi = c * MathF.Tau / segments;
                float cp = MathF.Cos(phi), sp = MathF.Sin(phi);
                var n = new Vector3(radial * cp, ny, radial * sp);
                var p = new Vector3(n.X * radius, yCenter + n.Y * radius, n.Z * radius);
                verts[r * cols + c] = new LitVertex3D(p, n, color);
            }
        }

        var indices = new List<uint>((rows - 1) * segments * 6);
        for (int r = 0; r < rows - 1; r++)
        {
            for (int c = 0; c < segments; c++)
            {
                uint a = (uint)(r * cols + c);
                uint b = a + 1;
                uint cc = (uint)((r + 1) * cols + c);
                uint d = cc + 1;
                indices.Add(a); indices.Add(cc); indices.Add(b);
                indices.Add(b); indices.Add(cc); indices.Add(d);
            }
        }

        return Mesh.Create<LitVertex3D>(verts, indices.ToArray());
    }

    /// <summary>
    /// Torus on the XZ plane (axis +Y).
    /// <paramref name="majorRadius"/> is the distance from the center to
    /// the tube center; <paramref name="minorRadius"/> is the tube radius.
    /// </summary>
    public static Mesh<LitVertex3D> Torus(
        Color color,
        float majorRadius = 0.5f, float minorRadius = 0.15f,
        int majorSegments = 32, int minorSegments = 16)
    {
        if (majorSegments < 3) throw new ArgumentOutOfRangeException(nameof(majorSegments));
        if (minorSegments < 3) throw new ArgumentOutOfRangeException(nameof(minorSegments));

        int majCols = majorSegments + 1;
        int minRows = minorSegments + 1;
        var verts = new LitVertex3D[majCols * minRows];
        for (int i = 0; i <= majorSegments; i++)
        {
            float phi = i * MathF.Tau / majorSegments;
            float cosP = MathF.Cos(phi), sinP = MathF.Sin(phi);
            var radial = new Vector3(cosP, 0f, sinP);
            var center = radial * majorRadius;
            for (int j = 0; j <= minorSegments; j++)
            {
                float theta = j * MathF.Tau / minorSegments;
                float cosT = MathF.Cos(theta), sinT = MathF.Sin(theta);
                var n = radial * cosT + Vector3.UnitY * sinT;
                var p = center + n * minorRadius;
                verts[i * minRows + j] = new LitVertex3D(p, n, color);
            }
        }

        var indices = new uint[majorSegments * minorSegments * 6];
        int k = 0;
        for (int i = 0; i < majorSegments; i++)
        {
            for (int j = 0; j < minorSegments; j++)
            {
                uint a = (uint)(i * minRows + j);
                uint b = a + 1;
                uint c = (uint)((i + 1) * minRows + j);
                uint d = c + 1;
                indices[k++] = a; indices[k++] = c; indices[k++] = b;
                indices[k++] = b; indices[k++] = c; indices[k++] = d;
            }
        }
        return Mesh.Create<LitVertex3D>(verts, indices);
    }

    // ----- Platonic solids (flat-shaded, lit) --------------------------

    /// <summary>
    /// Regular tetrahedron with the given circumradius (distance from
    /// center to each vertex). Per-face normals.
    /// </summary>
    public static Mesh<LitVertex3D> Tetrahedron(Color color, float radius = 0.5f)
    {
        // Vertices of a tetrahedron inscribed in a cube of side 2; scale
        // to the requested circumradius (cube-corner length = sqrt(3)).
        float s = radius / MathF.Sqrt(3f);
        var v0 = new Vector3( 1,  1,  1) * s;
        var v1 = new Vector3( 1, -1, -1) * s;
        var v2 = new Vector3(-1,  1, -1) * s;
        var v3 = new Vector3(-1, -1,  1) * s;
        return BuildFlatShaded(color,
            [v1, v2, v3,
             v0, v3, v2,
             v0, v1, v3,
             v0, v2, v1]);
    }

    /// <summary>Regular octahedron (six vertices on the axes). Per-face normals.</summary>
    public static Mesh<LitVertex3D> Octahedron(Color color, float radius = 0.5f)
    {
        var px = new Vector3( radius, 0, 0);
        var nx = new Vector3(-radius, 0, 0);
        var py = new Vector3(0,  radius, 0);
        var ny = new Vector3(0, -radius, 0);
        var pz = new Vector3(0, 0,  radius);
        var nz = new Vector3(0, 0, -radius);
        return BuildFlatShaded(color,
            [py, px, pz,  py, pz, nx,  py, nx, nz,  py, nz, px,
             ny, pz, px,  ny, nx, pz,  ny, nz, nx,  ny, px, nz]);
    }

    /// <summary>Regular icosahedron (20 faces, flat-shaded).</summary>
    public static Mesh<LitVertex3D> Icosahedron(Color color, float radius = 0.5f)
    {
        BuildIcosphere(0, out var positions, out var indices);
        var tris = new Vector3[indices.Length];
        for (int i = 0; i < indices.Length; i++)
            tris[i] = positions[indices[i]] * radius;
        return BuildFlatShaded(color, tris);
    }

    // Builds a flat-shaded mesh from a flat triangle-list of positions.
    // Computes one face normal per triangle and assigns it to all three
    // vertices, so each face shades uniformly.
    private static Mesh<LitVertex3D> BuildFlatShaded(Color color, ReadOnlySpan<Vector3> triangles)
    {
        var verts = new LitVertex3D[triangles.Length];
        for (int i = 0; i < triangles.Length; i += 3)
        {
            var a = triangles[i];
            var b = triangles[i + 1];
            var c = triangles[i + 2];
            var n = Vector3.Normalize(Vector3.Cross(b - a, c - a));
            verts[i + 0] = new LitVertex3D(a, n, color);
            verts[i + 1] = new LitVertex3D(b, n, color);
            verts[i + 2] = new LitVertex3D(c, n, color);
        }
        return Mesh.Create<LitVertex3D>(verts);
    }

    // ----- Lines (unlit, ColorVertex3D) --------------------------------

    /// <summary>
    /// XYZ axes gizmo: red X, green Y, blue Z line segments from the
    /// origin to <paramref name="length"/> along each axis.
    /// Topology: <see cref="Topology.LineList"/>.
    /// </summary>
    public static Mesh<ColorVertex3D> Axes(float length = 1f)
    {
        var red   = new Color(255, 64, 64);
        var green = new Color(64, 255, 64);
        var blue  = new Color(64, 128, 255);
        return Mesh.Create<ColorVertex3D>(
            [
                new(Vector3.Zero, red),    new(new Vector3(length, 0, 0), red),
                new(Vector3.Zero, green),  new(new Vector3(0, length, 0), green),
                new(Vector3.Zero, blue),   new(new Vector3(0, 0, length), blue),
            ],
            topology: Topology.LineList);
    }

    /// <summary>
    /// Wireframe ground grid on the XZ plane.
    /// <paramref name="cellsPerSide"/> × <paramref name="cellsPerSide"/>
    /// cells, each <paramref name="cellSize"/> across, centered on origin.
    /// Topology: <see cref="Topology.LineList"/>.
    /// </summary>
    public static Mesh<ColorVertex3D> Grid(int cellsPerSide = 10, float cellSize = 1f, Color? color = null)
    {
        if (cellsPerSide < 1) throw new ArgumentOutOfRangeException(nameof(cellsPerSide));

        var c = color ?? new Color(80, 80, 80);
        float half = cellsPerSide * cellSize * 0.5f;
        int linesPerAxis = cellsPerSide + 1;
        var verts = new ColorVertex3D[linesPerAxis * 4];
        int k = 0;
        for (int i = 0; i < linesPerAxis; i++)
        {
            float t = -half + i * cellSize;
            // Line parallel to X axis at z=t.
            verts[k++] = new ColorVertex3D(new Vector3(-half, 0, t), c);
            verts[k++] = new ColorVertex3D(new Vector3( half, 0, t), c);
            // Line parallel to Z axis at x=t.
            verts[k++] = new ColorVertex3D(new Vector3(t, 0, -half), c);
            verts[k++] = new ColorVertex3D(new Vector3(t, 0,  half), c);
        }
        return Mesh.Create<ColorVertex3D>(verts, topology: Topology.LineList);
    }

    /// <summary>
    /// Wireframe outline of a box (12 line segments).
    /// Topology: <see cref="Topology.LineList"/>.
    /// </summary>
    public static Mesh<ColorVertex3D> WireBox(Color color, Vector3? size = null)
    {
        var s = (size ?? Vector3.One) * 0.5f;
        var p = new Vector3[]
        {
            new(-s.X, -s.Y, -s.Z), new( s.X, -s.Y, -s.Z),
            new( s.X,  s.Y, -s.Z), new(-s.X,  s.Y, -s.Z),
            new(-s.X, -s.Y,  s.Z), new( s.X, -s.Y,  s.Z),
            new( s.X,  s.Y,  s.Z), new(-s.X,  s.Y,  s.Z),
        };
        // 12 edges -> 24 vertex slots in LineList layout.
        ReadOnlySpan<int> edges =
        [
            0,1, 1,2, 2,3, 3,0, // back face
            4,5, 5,6, 6,7, 7,4, // front face
            0,4, 1,5, 2,6, 3,7, // connectors
        ];
        var verts = new ColorVertex3D[edges.Length];
        for (int i = 0; i < edges.Length; i++)
            verts[i] = new ColorVertex3D(p[edges[i]], color);
        return Mesh.Create<ColorVertex3D>(verts, topology: Topology.LineList);
    }
}
