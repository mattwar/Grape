using System.Collections.Immutable;

namespace Blitter.Shaders;

/// <summary>Stage a uniform slot belongs to.</summary>
public enum ShaderArgStage
{
    Vertex,
    Fragment,
}

/// <summary>The shader-side type of a uniform slot.</summary>
public enum ShaderArgKind
{
    /// <summary>Single 32-bit float.</summary>
    Float,
    /// <summary>Two-component float vector (8 bytes).</summary>
    Float2,
    /// <summary>Three-component float vector (12 bytes).</summary>
    Float3,
    /// <summary>Four-component float vector (16 bytes).</summary>
    Float4,
    /// <summary>Single 32-bit signed integer.</summary>
    Int,
    /// <summary>Single 32-bit unsigned integer.</summary>
    UInt,
    /// <summary>4×4 float matrix (64 bytes).</summary>
    Matrix4x4,
}

/// <summary>One uniform slot: which stage, which slot index, and what type it is.</summary>
public sealed record ShaderArgElement(ShaderArgStage Stage, int Slot, ShaderArgKind Kind)
{
    /// <summary>Size in bytes of the data the renderer pushes for this slot.</summary>
    public int Size => Kind switch
    {
        ShaderArgKind.Float     => 4,
        ShaderArgKind.Float2    => 8,
        ShaderArgKind.Float3    => 12,
        ShaderArgKind.Float4    => 16,
        ShaderArgKind.Int       => 4,
        ShaderArgKind.UInt      => 4,
        ShaderArgKind.Matrix4x4 => 64,
        _ => throw new ArgumentOutOfRangeException(nameof(Kind)),
    };
}

/// <summary>
/// Describes the per-draw arguments a <see cref="ShaderSet{TVertex,TArgs}"/>
/// expects. Each <see cref="ShaderArgElement"/> corresponds to one field of the
/// matching <c>TArgs</c> struct, in declaration order; the renderer reads
/// each field's bytes and pushes them to the named (stage, slot).
/// </summary>
public sealed record ShaderArgsLayout(ImmutableArray<ShaderArgElement> Elements)
{
    public ShaderArgsLayout(params ShaderArgElement[] elements)
        : this(elements.ToImmutableArray()) { }

    /// <summary>Total byte length the matching <c>TArgs</c> struct must have.</summary>
    public int TotalSize
    {
        get
        {
            int total = 0;
            foreach (var e in Elements) total += e.Size;
            return total;
        }
    }
}
