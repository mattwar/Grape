using System.Numerics;

namespace Blitter;

/// <summary>
/// Marker interface for 2D vertex types that expose a model-space
/// position. Implemented by the built-in <see cref="Vertex2D"/>. Lets
/// generic helpers (bounds, picking, transforms) read positions out of
/// any compatible mesh without per-type overloads.
/// </summary>
public interface IPositionVertex2D
{
    /// <summary>The vertex's model-space position.</summary>
    Vector2 Position { get; }
}
