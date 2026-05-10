using System.Numerics;

namespace Blitter;

/// <summary>
/// Draw shapes to be displayed as an overlay in the renderer with <see cref="Renderer3D.DebugDrawEnabled"/> set.
/// </summary>
public static class DebugDraw
{
    // Single shared line buffer (LineList topology, ColorVertex3D).
    // Producers append under _gate; the active renderer swaps the whole
    // list out on flush so its upload doesn't block further appends.
    private static List<ColorVertex3D> _lines = new(capacity: 4096);
    private static readonly object _gate = new();

    // Ref-counted: incremented when a renderer enables, decremented on
    // disable. When zero, all Draw* calls early-out so the static is
    // free in release builds where no one is consuming.
    internal static int ConsumerCount;

    /// <summary>
    /// True when at least one renderer has <see cref="Renderer3D.DebugDrawEnabled"/> set.
    /// </summary>
    // Draw calls are no-ops when this is false.
    public static bool IsActive => Volatile.Read(ref ConsumerCount) > 0;

    // ---- Primitives ----------------------------------------------------

    /// <summary>Queues a line segment from <paramref name="a"/> to <paramref name="b"/>.</summary>
    public static void DrawLine(Vector3 a, Vector3 b, Color color)
    {
        if (!IsActive) return;
        lock (_gate)
        {
            _lines.Add(new ColorVertex3D(a, color));
            _lines.Add(new ColorVertex3D(b, color));
        }
    }

    /// <summary>Queues a ray of <paramref name="length"/> units from <paramref name="origin"/> in <paramref name="direction"/>.</summary>
    public static void DrawRay(Vector3 origin, Vector3 direction, Color color, float length = 1f)
    {
        if (!IsActive) return;
        var d = direction.LengthSquared() > 0 ? Vector3.Normalize(direction) : Vector3.Zero;
        DrawLine(origin, origin + d * length, color);
    }

    /// <summary>Queues an XYZ gizmo at <paramref name="origin"/>: red along +X, green along +Y, blue along +Z.</summary>
    public static void DrawAxes(Vector3 origin, float length = 1f)
    {
        if (!IsActive) return;
        DrawLine(origin, origin + Vector3.UnitX * length, Color.Red);
        DrawLine(origin, origin + Vector3.UnitY * length, Color.Green);
        DrawLine(origin, origin + Vector3.UnitZ * length, Color.Blue);
    }

    /// <summary>Queues the edges of an axis-aligned box from <paramref name="min"/> to <paramref name="max"/>.</summary>
    public static void DrawBox(Vector3 min, Vector3 max, Color color)
    {
        if (!IsActive) return;

        var c000 = new Vector3(min.X, min.Y, min.Z);
        var c100 = new Vector3(max.X, min.Y, min.Z);
        var c010 = new Vector3(min.X, max.Y, min.Z);
        var c110 = new Vector3(max.X, max.Y, min.Z);
        var c001 = new Vector3(min.X, min.Y, max.Z);
        var c101 = new Vector3(max.X, min.Y, max.Z);
        var c011 = new Vector3(min.X, max.Y, max.Z);
        var c111 = new Vector3(max.X, max.Y, max.Z);

        lock (_gate)
        {
            // Bottom face (y = min.Y)
            AddLine(c000, c100, color);
            AddLine(c100, c101, color);
            AddLine(c101, c001, color);
            AddLine(c001, c000, color);
            // Top face (y = max.Y)
            AddLine(c010, c110, color);
            AddLine(c110, c111, color);
            AddLine(c111, c011, color);
            AddLine(c011, c010, color);
            // Vertical edges
            AddLine(c000, c010, color);
            AddLine(c100, c110, color);
            AddLine(c101, c111, color);
            AddLine(c001, c011, color);
        }
    }

    /// <summary>Queues a box centered on <paramref name="center"/> with the given <paramref name="size"/>.</summary>
    public static void DrawBoxCentered(Vector3 center, Vector3 size, Color color)
    {
        var half = size * 0.5f;
        DrawBox(center - half, center + half, color);
    }

    /// <summary>
    /// Queues a wireframe sphere at <paramref name="center"/>.
    /// </summary>
    // Approximated with three great circles -- enough for "where is
    // this thing", not a precise surface.
    public static void DrawSphere(Vector3 center, float radius, Color color, int segments = 24)
    {
        if (!IsActive) return;
        if (segments < 3) segments = 3;

        lock (_gate)
        {
            DrawCircleNoLock(center, radius, Vector3.UnitX, Vector3.UnitY, color, segments); // XY
            DrawCircleNoLock(center, radius, Vector3.UnitY, Vector3.UnitZ, color, segments); // YZ
            DrawCircleNoLock(center, radius, Vector3.UnitX, Vector3.UnitZ, color, segments); // XZ
        }
    }

    // ---- Renderer integration -----------------------------------------

    // Atomically takes the current line buffer and leaves an empty
    // (but capacity-preserving) buffer in its place. Only the active
    // renderer should call this, once per frame; the caller owns the
    // returned list afterwards.
    internal static List<ColorVertex3D> TakeLines()
    {
        lock (_gate)
        {
            if (_lines.Count == 0)
                return _lines; // empty: cheap to return as-is, no swap needed.

            var snapshot = _lines;
            _lines = new List<ColorVertex3D>(snapshot.Capacity);
            return snapshot;
        }
    }

    // ---- Helpers ------------------------------------------------------

    private static void AddLine(Vector3 a, Vector3 b, Color color)
    {
        _lines.Add(new ColorVertex3D(a, color));
        _lines.Add(new ColorVertex3D(b, color));
    }

    private static void DrawCircleNoLock(
        Vector3 center, float radius, Vector3 axisU, Vector3 axisV, Color color, int segments)
    {
        var prev = center + axisU * radius;
        for (int i = 1; i <= segments; i++)
        {
            float t = (float)i / segments * MathF.Tau;
            var next = center + (axisU * MathF.Cos(t) + axisV * MathF.Sin(t)) * radius;
            AddLine(prev, next, color);
            prev = next;
        }
    }
}
