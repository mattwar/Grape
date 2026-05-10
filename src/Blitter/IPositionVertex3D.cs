using System.Numerics;

namespace Blitter;

/// <summary>
/// Marker interface for 3D vertex types that expose a model-space
/// position. Implemented by the built-in <c>Vertex3D</c>, <c>ColorVertex3D</c>,
/// <c>TextureVertex3D</c>, <c>LitVertex3D</c>, and <c>LitTextureVertex3D</c>.
/// Lets generic helpers (bounds, raycast, transforms) read positions out of
/// any compatible mesh without each helper needing per-type overloads.
/// </summary>
public interface IPositionVertex3D
{
    /// <summary>The vertex's model-space position.</summary>
    Vector3 Position { get; }
}
