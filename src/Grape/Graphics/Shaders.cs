using System.Numerics;

namespace Grape;

/// <summary>
/// The vertex/fragment shader pairs Grape ships with. Each shader is
/// described as inline HLSL source and only compiled to the format the GPU
/// device needs on first draw, courtesy of <see cref="Shader"/>'s lazy
/// compilation pipeline. Construction is essentially free -- the cost is
/// just a few string references and small layout objects -- so all sets are
/// pre-built as singletons at class-load time.
/// </summary>
/// <remarks>
/// <para>
/// Resource bindings follow SDL3 GPU's SPIR-V conventions: vertex uniform
/// buffers live in <c>register(b0, space1)</c>; fragment textures and
/// samplers share <c>space2</c>. <c>SDL_shadercross</c> rewrites these
/// bindings appropriately for DXIL and MSL when those formats are produced.
/// </para>
/// </remarks>
public static class Shaders
{
    // ---- HLSL sources (kept as strings so we don't need any embedded
    // resources or build-time compilation steps). The DXC -> SPIR-V path is
    // run lazily on first GetCode/GetResources call.

    private const string PositionColorVertHlsl = 
        """
        struct Input
        {
            float3 Position : TEXCOORD0;
            float4 Color    : TEXCOORD1;
        };

        struct Output
        {
            float4 Color    : TEXCOORD0;
            float4 Position : SV_Position;
        };

        Output main(Input input)
        {
            Output output;
            output.Color    = input.Color;
            output.Position = float4(input.Position, 1.0f);
            return output;
        }
        """;

    private const string PositionColorWithTransformVertHlsl = 
        """
        cbuffer UBO : register(b0, space1)
        {
            float4x4 transform : packoffset(c0);
        };

        struct Input
        {
            float3 Position : TEXCOORD0;
            float4 Color    : TEXCOORD1;
        };

        struct Output
        {
            float4 Color    : TEXCOORD0;
            float4 Position : SV_Position;
        };

        Output main(Input input)
        {
            Output output;
            output.Color    = input.Color;
            output.Position = mul(transform, float4(input.Position, 1.0f));
            return output;
        }
        """;

    private const string PositionTextureVertHlsl = 
        """
        struct Input
        {
            float3 Position : TEXCOORD0;
            float2 TexCoord : TEXCOORD1;
        };

        struct Output
        {
            float2 TexCoord : TEXCOORD0;
            float4 Position : SV_Position;
        };

        Output main(Input input)
        {
            Output output;
            output.TexCoord = input.TexCoord;
            output.Position = float4(input.Position, 1.0f);
            return output;
        }
        """;

    private const string PositionTextureWithTransformVertHlsl = 
        """
        cbuffer UniformBlock : register(b0, space1)
        {
            float4x4 MatrixTransform : packoffset(c0);
        };

        struct Input
        {
            float4 Position : TEXCOORD0;
            float2 TexCoord : TEXCOORD1;
        };

        struct Output
        {
            float2 TexCoord : TEXCOORD0;
            float4 Position : SV_Position;
        };

        Output main(Input input)
        {
            Output output;
            output.TexCoord = input.TexCoord;
            output.Position = mul(MatrixTransform, input.Position);
            return output;
        }
        """;

    private const string SolidColorFragHlsl = 
    """
        float4 main(float4 Color : TEXCOORD0) : SV_Target0
        {
            return Color;
        }
        """;

    private const string PositionTextureFragHlsl = """
        Texture2D<float4> Texture : register(t0, space2);
        SamplerState      Sampler : register(s0, space2);

        float4 main(float2 TexCoord : TEXCOORD0) : SV_Target0
        {
            return Texture.Sample(Sampler, TexCoord);
        }
        """;

    // Position-only vertex shader -- passes the position through unchanged.
    private const string PositionVertHlsl = """
        struct Input  { float3 Position : TEXCOORD0; };
        struct Output { float4 Position : SV_Position; };

        Output main(Input input)
        {
            Output output;
            output.Position = float4(input.Position, 1.0f);
            return output;
        }
        """;

    // Position-only vertex shader with a per-draw 4x4 transform matrix.
    private const string PositionWithTransformVertHlsl = """
        cbuffer UBO : register(b0, space1)
        {
            float4x4 transform : packoffset(c0);
        };

        struct Input  { float3 Position : TEXCOORD0; };
        struct Output { float4 Position : SV_Position; };

        Output main(Input input)
        {
            Output output;
            output.Position = mul(transform, float4(input.Position, 1.0f));
            return output;
        }
        """;

    // Fragment shader that emits a single per-draw color from a uniform
    // buffer. Fragment uniform buffers live in space3 by SDL3 GPU's
    // SPIR-V binding convention.
    private const string SolidColorUniformFragHlsl = """
        cbuffer ColorBlock : register(b0, space3)
        {
            float4 Color : packoffset(c0);
        };

        float4 main() : SV_Target0
        {
            return Color;
        }
        """;

    // Fragment shader that emits opaque white. Useful for proving geometry
    // without paying for any per-vertex or per-draw color data.
    private const string WhiteFragHlsl = """
        float4 main() : SV_Target0
        {
            return float4(1.0f, 1.0f, 1.0f, 1.0f);
        }
        """;

    // ---- Per-stage Shader instances. Several sets share a fragment stage
    // (e.g. SolidColor is used by both PositionColor variants), so we cache
    // each stage shader as a singleton to keep GPU upload bookkeeping
    // identity-keyed and cheap.

    private static readonly Shader PositionColorVert =
        new(ShaderKind.Vertex, PositionColorVertHlsl);

    private static readonly Shader PositionColorWithTransformVert =
        new(ShaderKind.Vertex, PositionColorWithTransformVertHlsl);

    private static readonly Shader PositionTextureVert =
        new(ShaderKind.Vertex, PositionTextureVertHlsl);

    private static readonly Shader PositionTextureWithTransformVert =
        new(ShaderKind.Vertex, PositionTextureWithTransformVertHlsl);

    private static readonly Shader SolidColorFrag =
        new(ShaderKind.Fragment, SolidColorFragHlsl);

    private static readonly Shader PositionTextureFrag =
        new(ShaderKind.Fragment, PositionTextureFragHlsl);

    private static readonly Shader PositionVert =
        new(ShaderKind.Vertex, PositionVertHlsl);

    private static readonly Shader PositionWithTransformVert =
        new(ShaderKind.Vertex, PositionWithTransformVertHlsl);

    private static readonly Shader SolidColorUniformFrag =
        new(ShaderKind.Fragment, SolidColorUniformFragHlsl);

    private static readonly Shader WhiteFrag =
        new(ShaderKind.Fragment, WhiteFragHlsl);

    /// <summary>
    /// A single-element <see cref="ShaderArgsLayout"/> describing a 4x4
    /// matrix at vertex slot 0 -- the convention shared by every built-in
    /// transform shader.
    /// </summary>
    private static readonly ShaderArgsLayout TransformLayout = new(
        new ShaderArgElement(ShaderArgStage.Vertex, 0, ShaderArgKind.Matrix4x4));

    /// <summary>
    /// Two-element layout: a vertex-stage 4x4 transform at slot 0 plus a
    /// fragment-stage <c>float4</c> color at slot 0. Pairs with
    /// <see cref="TransformAndColorArgs"/>.
    /// </summary>
    private static readonly ShaderArgsLayout TransformAndColorLayout = new(
        new ShaderArgElement(ShaderArgStage.Vertex,   0, ShaderArgKind.Matrix4x4),
        new ShaderArgElement(ShaderArgStage.Fragment, 0, ShaderArgKind.Float4));

    /// <summary>
    /// Position-only vertices, no transform; emits opaque white. Positions
    /// must already be in normalized device coordinates.
    /// </summary>
    public static ShaderSet<Vertex3D> Position { get; } =
        new(PositionVert, WhiteFrag, Vertex3D.ShaderVertexLayout);

    /// <summary>
    /// Position-only vertices, transformed by a per-draw 4x4 matrix; emits
    /// opaque white.
    /// </summary>
    public static ShaderSet<Vertex3D, Matrix4x4> PositionWithTransform { get; } =
        new(PositionWithTransformVert, WhiteFrag, Vertex3D.ShaderVertexLayout, TransformLayout);

    /// <summary>
    /// Position-only vertices, transformed by a per-draw 4x4 matrix, with a
    /// per-draw fragment color. Pair with
    /// <see cref="TransformAndColorArgs"/>.
    /// </summary>
    public static ShaderSet<Vertex3D, TransformAndColorArgs> PositionWithTransformAndColor { get; } =
        new(PositionWithTransformVert, SolidColorUniformFrag, Vertex3D.ShaderVertexLayout, TransformAndColorLayout);

    /// <summary>
    /// Draws each vertex at its position with its baked color, with no
    /// transformation. Positions must already be in normalized device
    /// coordinates (the visible cube is -1 to 1 on each axis). Useful for
    /// screen-space drawing or testing.
    /// </summary>
    public static ShaderSet<ColorVertex3D> PositionColor { get; } =
        new(PositionColorVert, SolidColorFrag, ColorVertex3D.ShaderVertexLayout);

    /// <summary>
    /// Draws each vertex at its position with its color, transforming the
    /// position by a per-draw 4x4 model-view-projection matrix.
    /// </summary>
    public static ShaderSet<ColorVertex3D, Matrix4x4> PositionColorWithTransform { get; } =
        new(PositionColorWithTransformVert, SolidColorFrag, ColorVertex3D.ShaderVertexLayout, TransformLayout);

    /// <summary>
    /// Draws each vertex at its position, sampling the bound texture using
    /// the vertex texture coordinate, with no transformation. Positions must
    /// already be in normalized device coordinates.
    /// </summary>
    public static ShaderSet<TextureVertex3D> PositionTexture { get; } =
        new(PositionTextureVert, PositionTextureFrag, TextureVertex3D.ShaderVertexLayout);

    /// <summary>
    /// Draws each vertex at its position transformed by a per-draw 4x4
    /// model-view-projection matrix, sampling the bound texture using the
    /// vertex texture coordinate.
    /// </summary>
    public static ShaderSet<TextureVertex3D, Matrix4x4> PositionTextureWithTransform { get; } =
        new(PositionTextureWithTransformVert, PositionTextureFrag, TextureVertex3D.ShaderVertexLayout, TransformLayout);
}
