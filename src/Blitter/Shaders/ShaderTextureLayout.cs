using System.Collections.Immutable;

namespace Blitter;

/// <summary>The shape of a single texture binding the shader expects.</summary>
public enum ShaderTextureDimension
{
    Texture2D,
    TextureCube,
    Texture3D,
    Texture2DArray,
}

/// <summary>
/// One texture slot in a shader's fragment-stage sampler layout. The slot
/// index is its position in the parent <see cref="ShaderTextureLayout"/>;
/// <see cref="Name"/> is engine-side metadata used by higher layers
/// (e.g. <c>Material</c>) to refer to the slot by purpose rather than index.
/// </summary>
public sealed record ShaderTextureSlot(string Name, ShaderTextureDimension Dimension);

/// <summary>
/// Describes the texture/sampler bindings a shader expects on its fragment
/// stage. The renderer validates the count (and, eventually, per-slot
/// dimension) of textures supplied to a draw call against this layout.
/// </summary>
public sealed record ShaderTextureLayout(ImmutableArray<ShaderTextureSlot> Slots)
{
    public ShaderTextureLayout(params ShaderTextureSlot[] slots)
        : this(slots.ToImmutableArray()) { }

    /// <summary>Number of texture slots the shader binds.</summary>
    public int Count => Slots.Length;

    /// <summary>A layout with no texture bindings.</summary>
    public static ShaderTextureLayout Empty { get; } = new(ImmutableArray<ShaderTextureSlot>.Empty);

    /// <summary>One 2D texture slot named <c>"texture"</c>. The default for textured shaders.</summary>
    public static ShaderTextureLayout SingleTexture2D { get; } =
        new(new ShaderTextureSlot("texture", ShaderTextureDimension.Texture2D));

    /// <summary>One cubemap slot named <c>"cubemap"</c>. Used by skybox-style shaders.</summary>
    public static ShaderTextureLayout SingleTextureCube { get; } =
        new(new ShaderTextureSlot("cubemap", ShaderTextureDimension.TextureCube));
}
