using System.Numerics;
using System.Runtime.CompilerServices;

namespace Blitter.Bits;

/// <summary>
/// The default <see cref="Materializer"/>: routes every supported
/// material kind to the appropriate built-in shader from
/// <see cref="Shaders"/>. Today only <see cref="LitTextureMaterial"/>
/// is wired up (to <see cref="Shaders.LitTexture"/> and, for instanced
/// draws, <see cref="Shaders.LitTextureInstanced"/>); other material
/// kinds throw <see cref="MaterializerNotSupportedException"/>.
/// </summary>
/// <remarks>
/// Stateless and process-shared via <see cref="Default"/>; user code
/// almost never needs to construct one explicitly. A
/// <see cref="LitTextureMaterial"/> with no
/// <see cref="LitTextureMaterial.DiffuseTexture"/> binds against a
/// 1×1 white placeholder image so the shader's sampler is always
/// populated. The placeholder is created on first use and lives for
/// the process lifetime.
/// </remarks>
public class StandardMaterializer : Materializer
{
    private static Image? s_whitePlaceholder;

    /// <summary>
    /// Process-shared instance used by the
    /// <c>Renderer3D.DrawMesh(mesh, material, ...)</c> /
    /// <c>Renderer3D.DrawModel(...)</c> extension methods when no
    /// materializer is supplied. Safe to use directly.
    /// </summary>
    public static StandardMaterializer Default { get; } = new();

    /// <summary>
    /// Default <see cref="LitTextureMaterial"/> (white, no texture)
    /// returned when callers omit the material argument on the
    /// <c>Renderer3D.DrawMesh</c> extension overloads. Sampler binds
    /// against the shared 1×1 white placeholder, so the mesh draws
    /// using its baked vertex colors.
    /// </summary>
    public override Material DefaultMaterial { get; } = new LitTextureMaterial();

    /// <inheritdoc/>
    public override void DrawMesh(
        Renderer3D renderer, Mesh mesh, Material material, in Matrix4x4 transform)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(mesh);
        ArgumentNullException.ThrowIfNull(material);

        switch (material)
        {
            case LitTextureMaterial lit:
                // Material.DiffuseColor is not honored: per-vertex tint
                // (baked by the OBJ loader) is what carries Kd today.
                // Hand-built parts with white vertices + a colored
                // material won't see the tint; revisit when materials
                // grow a per-draw uniform tier.
                var texture = lit.DiffuseTexture ?? GetWhitePlaceholder();
                var args = new LitArgs(transform);
                MeshDispatcher.For(mesh).DrawTextured(
                    renderer, mesh, texture, Shaders.LitTexture, in args);
                break;

            case PbrMaterial pbr:
                DrawPbrMesh(renderer, mesh, pbr, in transform);
                break;

            default:
                throw new MaterializerNotSupportedException(mesh, material);
        }
    }

    private static void DrawPbrMesh(
        Renderer3D renderer, Mesh mesh, PbrMaterial pbr, in Matrix4x4 transform)
    {
        // A single 1x1 white image plugs every "missing texture" slot:
        // the shader multiplies each sample by the matching factor, so
        // white reduces to "use the factor unchanged". Inline-array
        // buffer keeps the four texture refs on the stack -- no per-draw
        // heap allocation -- and we hand a Span into it to the renderer.
        var white = GetWhitePlaceholder();
        PbrTextureBuffer buffer = default;
        buffer[0] = pbr.BaseColorTexture ?? white;
        buffer[1] = pbr.MetallicRoughnessTexture ?? white;
        buffer[2] = pbr.OcclusionTexture ?? white;
        buffer[3] = pbr.EmissiveTexture ?? white;

        var args = new PbrArgs
        {
            Model = transform,
            ViewProjection = Matrix4x4.Identity,
            BaseColorFactor = pbr.BaseColor,
            MaterialFactors = new Vector4(pbr.Metallic, pbr.Roughness, pbr.OcclusionStrength, 0f),
            EmissiveFactor = pbr.Emissive,
        };
        MeshDispatcher.For(mesh).DrawMultiTextured(
            renderer, mesh, buffer[..], PbrShaders.LitPbr, in args);
    }

    // 4-slot stack buffer for PBR texture binding. InlineArray gives us a
    // Span<Image> over the fields without allocating; the renderer copies
    // the references out before the span dies.
    [InlineArray(4)]
    private struct PbrTextureBuffer
    {
        private Image _slot0;
    }

    /// <inheritdoc/>
    public override void DrawMesh<TInstance>(
        Renderer3D renderer, Mesh mesh, Material material, ReadOnlySpan<TInstance> instances)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(mesh);
        ArgumentNullException.ThrowIfNull(material);

        // Today's only built-in instanced surface shader pairs
        // LitTextureMaterial with TransformAndColorInstance. Other
        // material kinds, or other per-instance struct shapes, need a
        // custom Materializer subclass that registers the matching
        // shader.
        if (material is LitTextureMaterial lit
            && typeof(TInstance) == typeof(TransformAndColorInstance))
        {
            var texture = lit.DiffuseTexture ?? GetWhitePlaceholder();
            var args = default(LitArgs); // Model unused; per-instance transform replaces it.
            MeshDispatcher.For(mesh).DrawTexturedInstanced(
                renderer, mesh, texture, Shaders.LitTextureInstanced, in args, instances);
            return;
        }

        throw new MaterializerNotSupportedException(mesh, material, typeof(TInstance));
    }

    private static Image GetWhitePlaceholder()
    {
        // Lazy + non-locked: racing threads might allocate two 1x1
        // images on first use and the loser's gets GC'd later. Cheap
        // enough that an Interlocked dance isn't worth the noise.
        return s_whitePlaceholder ??= CreateWhitePlaceholder();
    }

    private static Image CreateWhitePlaceholder()
    {
        var image = Image.Create(1, 1, PixelFormat.ABGR8888);
        image.SetPixel(0, 0, Color.White);
        return image;
    }
}
