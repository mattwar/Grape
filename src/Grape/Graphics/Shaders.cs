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

    // Skybox vertex shader. Takes a unit cube vertex in object space
    // and:
    //   1. Passes the raw position through as the cubemap sample
    //      direction. Since the cube is centred on the origin, the
    //      vector from the centre to each vertex is exactly the world
    //      direction the corresponding pixel "looks toward".
    //   2. Projects the vertex with the supplied transform, then
    //      forces clip-space Z to equal W. After the perspective
    //      divide that produces depth 1.0 (the far plane), so the
    //      skybox is rendered behind everything else even with a
    //      Less-or-Equal depth test. Caller is expected to pass a
    //      view-projection that has the camera *translation* zeroed
    //      out so the skybox stays centred on the camera regardless
    //      of where the camera moves; Camera3D.GetSkyboxViewProjection
    //      builds that for you.
    private const string SkyboxVertHlsl = """
        cbuffer UBO : register(b0, space1)
        {
            float4x4 transform : packoffset(c0);
        };

        struct Input
        {
            float3 Position : TEXCOORD0;
        };

        struct Output
        {
            float3 Direction : TEXCOORD0;
            float4 Position  : SV_Position;
        };

        Output main(Input input)
        {
            Output output;
            output.Direction = input.Position;
            float4 clip = mul(transform, float4(input.Position, 1.0f));
            // Push the skybox onto the far plane: clip.z/clip.w = 1
            // after the perspective divide. We use clip.w for both so
            // any vertex at any depth resolves to depth 1 in NDC.
            output.Position = float4(clip.xy, clip.w, clip.w);
            return output;
        }
        """;

    // Skybox fragment shader. Samples the cubemap by the interpolated
    // direction. SDL3-GPU/HLSL note: the cubemap binding is a
    // TextureCube; SDL_shadercross translates this to the equivalent
    // SPIR-V/MSL/DXIL cube-sampled-image. The vertex shader emits the
    // direction in left-handed cubemap-space convention (+X right,
    // +Y up, +Z forward); the negate on .z below converts a normal
    // right-handed System.Numerics camera direction so the skybox
    // doesn't appear mirrored front-to-back. If your scene already
    // uses a left-handed camera convention you can drop the negate.
    private const string SkyboxFragHlsl = """
        TextureCube<float4> Cubemap : register(t0, space2);
        SamplerState        Sampler : register(s0, space2);

        float4 main(float3 Direction : TEXCOORD0) : SV_Target0
        {
            float3 dir = float3(Direction.x, Direction.y, -Direction.z);
            return Cubemap.Sample(Sampler, dir);
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

    // Lit color vertex shader. Per-vertex Lambertian shading (Gouraud):
    // the lighting is computed at each vertex and the resulting color is
    // interpolated across the triangle by the rasterizer; the fragment
    // stage is just the existing SolidColorFrag passthrough.
    //
    // Uniform layout (matches LitArgs / LitColorArgsLayout):
    //   b0 space1 = Model            (mat4)
    //   b1 space1 = ViewProjection   (mat4)
    //   b2 space1 = AmbientLight     (rgba, alpha unused)
    //   b3 space1 = LightDirection   (xyz=direction TO light in world space; w unused)
    //   b4 space1 = LightColor       (rgba, alpha unused)
    //
    // Normals are transformed by the upper-3x3 of the model matrix. This
    // is correct for rotation + uniform scale + translation; non-uniform
    // scale would need a true inverse-transpose. That's a fine starting
    // assumption for the built-in lit shader -- callers that need the
    // full treatment can author a custom shader.
    private const string LitColorVertHlsl = """
        cbuffer Model    : register(b0, space1) { float4x4 model;          };
        cbuffer VP       : register(b1, space1) { float4x4 viewProjection; };
        cbuffer Ambient  : register(b2, space1) { float4   ambient;        };
        cbuffer LightDir : register(b3, space1) { float4   lightDir;       };
        cbuffer LightCol : register(b4, space1) { float4   lightColor;     };

        struct Input
        {
            float3 Position : TEXCOORD0;
            float3 Normal   : TEXCOORD1;
            float4 Color    : TEXCOORD2;
        };

        struct Output
        {
            float4 Color    : TEXCOORD0;
            float4 Position : SV_Position;
        };

        Output main(Input input)
        {
            Output output;
            float4 worldPos    = mul(model, float4(input.Position, 1.0f));
            output.Position    = mul(viewProjection, worldPos);

            float3 worldNormal = normalize(mul((float3x3)model, input.Normal));
            // Light direction may be zero (no light configured). Guard
            // against the resulting NaN from normalize() by adding the
            // contribution only when the supplied direction is non-zero.
            float  dirLen      = length(lightDir.xyz);
            float3 L           = dirLen > 0.0001f ? lightDir.xyz / dirLen : float3(0, 0, 0);
            float  NdotL       = saturate(dot(worldNormal, L));
            float3 lit         = ambient.rgb + lightColor.rgb * NdotL;

            output.Color       = float4(input.Color.rgb * lit, input.Color.a);
            return output;
        }
        """;

    // ---- Instanced shaders. Per-vertex inputs take the same TEXCOORDn
    // slots as the non-instanced variants. Per-instance inputs follow on
    // higher slots: a float4x4 transform consumes four consecutive
    // semantic indices (one per row), then the float4 tint. The per-call
    // uniform is the camera view-projection matrix; each instance's final
    // clip-space position is VP * InstanceTransform * vertexPosition.

    private const string PositionInstancedVertHlsl = """
        cbuffer UBO : register(b0, space1)
        {
            float4x4 ViewProjection : packoffset(c0);
        };

        struct Input
        {
            float3 Position          : TEXCOORD0;
            float4x4 InstanceTransform : TEXCOORD1;
            float4 InstanceTint      : TEXCOORD5;
        };

        struct Output
        {
            float4 Color    : TEXCOORD0;
            float4 Position : SV_Position;
        };

        Output main(Input input)
        {
            Output output;
            output.Color    = input.InstanceTint;
            output.Position = mul(ViewProjection, mul(input.InstanceTransform, float4(input.Position, 1.0f)));
            return output;
        }
        """;

    private const string PositionColorInstancedVertHlsl = """
        cbuffer UBO : register(b0, space1)
        {
            float4x4 ViewProjection : packoffset(c0);
        };

        struct Input
        {
            float3 Position          : TEXCOORD0;
            float4 Color             : TEXCOORD1;
            float4x4 InstanceTransform : TEXCOORD2;
            float4 InstanceTint      : TEXCOORD6;
        };

        struct Output
        {
            float4 Color    : TEXCOORD0;
            float4 Position : SV_Position;
        };

        Output main(Input input)
        {
            Output output;
            output.Color    = input.Color * input.InstanceTint;
            output.Position = mul(ViewProjection, mul(input.InstanceTransform, float4(input.Position, 1.0f)));
            return output;
        }
        """;

    private const string PositionTextureInstancedVertHlsl = """
        cbuffer UBO : register(b0, space1)
        {
            float4x4 ViewProjection : packoffset(c0);
        };

        struct Input
        {
            float3 Position          : TEXCOORD0;
            float2 TexCoord          : TEXCOORD1;
            float4x4 InstanceTransform : TEXCOORD2;
            float4 InstanceTint      : TEXCOORD6;
        };

        struct Output
        {
            float2 TexCoord : TEXCOORD0;
            float4 Tint     : TEXCOORD1;
            float4 Position : SV_Position;
        };

        Output main(Input input)
        {
            Output output;
            output.TexCoord = input.TexCoord;
            output.Tint     = input.InstanceTint;
            output.Position = mul(ViewProjection, mul(input.InstanceTransform, float4(input.Position, 1.0f)));
            return output;
        }
        """;

    // Pairs with PositionTextureInstancedVertHlsl: samples the texture
    // and multiplies by the per-instance tint piped through from the
    // vertex stage.
    private const string PositionTextureTintedFragHlsl = """
        Texture2D<float4> Texture : register(t0, space2);
        SamplerState      Sampler : register(s0, space2);

        struct Input
        {
            float2 TexCoord : TEXCOORD0;
            float4 Tint     : TEXCOORD1;
        };

        float4 main(Input input) : SV_Target0
        {
            return Texture.Sample(Sampler, input.TexCoord) * input.Tint;
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

    private static readonly Shader SkyboxVert =
        new(ShaderKind.Vertex, SkyboxVertHlsl);

    private static readonly Shader SkyboxFrag =
        new(ShaderKind.Fragment, SkyboxFragHlsl);

    private static readonly Shader PositionVert =
        new(ShaderKind.Vertex, PositionVertHlsl);

    private static readonly Shader PositionWithTransformVert =
        new(ShaderKind.Vertex, PositionWithTransformVertHlsl);

    private static readonly Shader SolidColorUniformFrag =
        new(ShaderKind.Fragment, SolidColorUniformFragHlsl);

    private static readonly Shader WhiteFrag =
        new(ShaderKind.Fragment, WhiteFragHlsl);

    private static readonly Shader LitColorVert =
        new(ShaderKind.Vertex, LitColorVertHlsl);

    private static readonly Shader PositionInstancedVert =
        new(ShaderKind.Vertex, PositionInstancedVertHlsl);

    private static readonly Shader PositionColorInstancedVert =
        new(ShaderKind.Vertex, PositionColorInstancedVertHlsl);

    private static readonly Shader PositionTextureInstancedVert =
        new(ShaderKind.Vertex, PositionTextureInstancedVertHlsl);

    private static readonly Shader PositionTextureTintedFrag =
        new(ShaderKind.Fragment, PositionTextureTintedFragHlsl);

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
    /// <see cref="TransformAndFColor"/>.
    /// </summary>
    private static readonly ShaderArgsLayout TransformAndColorLayout = new(
        new ShaderArgElement(ShaderArgStage.Vertex,   0, ShaderArgKind.Matrix4x4),
        new ShaderArgElement(ShaderArgStage.Fragment, 0, ShaderArgKind.Float4));

    /// <summary>
    /// Five-slot layout matching <see cref="LitArgs"/>. All slots live on
    /// the vertex stage because the built-in lit shader does Lambertian
    /// shading per-vertex (Gouraud); a per-pixel variant would split the
    /// lighting slots onto the fragment stage instead.
    /// </summary>
    private static readonly ShaderArgsLayout LitColorArgsLayout = new(
        new ShaderArgElement(ShaderArgStage.Vertex, 0, ShaderArgKind.Matrix4x4), // Model
        new ShaderArgElement(ShaderArgStage.Vertex, 1, ShaderArgKind.Matrix4x4), // ViewProjection
        new ShaderArgElement(ShaderArgStage.Vertex, 2, ShaderArgKind.Float4),    // AmbientLight
        new ShaderArgElement(ShaderArgStage.Vertex, 3, ShaderArgKind.Float4),    // LightDirection
        new ShaderArgElement(ShaderArgStage.Vertex, 4, ShaderArgKind.Float4));   // LightColor

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
    public static ShaderSet<Vertex3D, Transform> PositionWithTransform { get; } =
        new(PositionWithTransformVert, WhiteFrag, Vertex3D.ShaderVertexLayout, TransformLayout);

    /// <summary>
    /// Position-only vertices, transformed by a per-draw 4x4 matrix, with a
    /// per-draw fragment color. Pair with
    /// <see cref="TransformAndFColor"/>.
    /// </summary>
    public static ShaderSet<Vertex3D, TransformAndFColor> PositionWithTransformAndColor { get; } =
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
    /// <remarks>
    /// The args type is <see cref="Transform"/> (a single
    /// <see cref="Matrix4x4"/> field) rather than a bare
    /// <see cref="Matrix4x4"/> so this shader works with
    /// <see cref="Renderer3D.DrawSceneMesh{TVertex,TArgs}(Mesh{TVertex},
    /// ShaderSet{TVertex,TArgs}, in TArgs)"/>: pass a model matrix and
    /// the renderer composes <see cref="Renderer3D.Camera"/> into it.
    /// Existing callers passing <c>model * viewProjection</c> continue
    /// to work via the implicit <see cref="Matrix4x4"/> → <see
    /// cref="Transform"/> conversion.
    /// </remarks>
    public static ShaderSet<ColorVertex3D, Transform> PositionColorWithTransform { get; } =
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
    public static ShaderSet<TextureVertex3D, Transform> PositionTextureWithTransform { get; } =
        new(PositionTextureWithTransformVert, PositionTextureFrag, TextureVertex3D.ShaderVertexLayout, TransformLayout);

    /// <summary>
    /// Lit color shader: per-vertex Lambertian shading from the renderer's
    /// <see cref="Renderer3D.AmbientLight"/> and
    /// <see cref="Renderer3D.DirectionalLight"/>, modulated by the
    /// per-vertex baked color. Pair with <see cref="LitVertex3D"/> and
    /// <see cref="LitArgs"/>: callers supply <see cref="LitArgs.Model"/>;
    /// the renderer fills in the view-projection and lighting fields
    /// through <see cref="IRenderArgs{TSelf}"/>.
    /// </summary>
    /// <remarks>
    /// Normals are transformed by the model matrix's upper-3x3 -- correct
    /// for rotation + uniform scale + translation. Non-uniform scales need
    /// a custom shader using a true inverse-transpose normal matrix.
    /// </remarks>
    public static ShaderSet<LitVertex3D, LitArgs> LitColor { get; } =
        new(LitColorVert, SolidColorFrag, LitVertex3D.ShaderVertexLayout, LitColorArgsLayout);

    /// <summary>
    /// Skybox shader: samples the bound <see cref="Cubemap"/> by the
    /// world-space direction implied by each vertex's position, with
    /// depth forced to the far plane so the result always renders
    /// behind everything else. Pair with a unit cube mesh whose
    /// vertices span [-1, 1] on each axis (e.g. the cube from
    /// <c>samples/IndexedCube.cs</c>) and pass
    /// <see cref="Camera3D.GetSkyboxViewProjection(float)"/> as the
    /// per-draw transform so the skybox follows the camera. Use
    /// <see cref="DepthMode.Default"/> -- the shader's depth output of
    /// 1.0 ensures the skybox draws behind opaque geometry without
    /// special depth state.
    /// </summary>
    public static ShaderSet<Vertex3D, Matrix4x4> Skybox { get; } =
        new(SkyboxVert, SkyboxFrag, Vertex3D.ShaderVertexLayout, TransformLayout);

    /// <summary>
    /// The per-instance vertex layout used by every built-in
    /// <c>*Instanced</c> shader: a 4x4 transform followed by an 8-bit
    /// RGBA color. Pairs with <see cref="TransformAndColor"/>.
    /// </summary>
    private static readonly ShaderVertexLayout TransformAndColorVertexLayout = new(
        ShaderVertexElementKind.Matrix4x4,
        ShaderVertexElementKind.Color4);

    /// <summary>
    /// Instanced variant of <see cref="PositionWithTransform"/>: draws the
    /// mesh once per <see cref="TransformAndColor"/> in the supplied span.
    /// The per-call uniform is the camera view-projection matrix; each
    /// instance contributes its own world transform and color.
    /// </summary>
    public static InstancedShaderSet<Vertex3D, Matrix4x4, TransformAndColor> PositionInstanced { get; } =
        new(PositionInstancedVert, SolidColorFrag,
            Vertex3D.ShaderVertexLayout,
            TransformAndColorVertexLayout,
            TransformLayout);

    /// <summary>
    /// Instanced variant of <see cref="PositionColorWithTransform"/>: the
    /// per-vertex color is multiplied by the per-instance color, so a
    /// white-color instance shows the mesh's baked colors unchanged.
    /// </summary>
    public static InstancedShaderSet<ColorVertex3D, Matrix4x4, TransformAndColor> PositionColorInstanced { get; } =
        new(PositionColorInstancedVert, SolidColorFrag,
            ColorVertex3D.ShaderVertexLayout,
            TransformAndColorVertexLayout,
            TransformLayout);

    /// <summary>
    /// Instanced variant of <see cref="PositionTextureWithTransform"/>:
    /// the texture sample is multiplied by the per-instance color.
    /// </summary>
    public static InstancedShaderSet<TextureVertex3D, Matrix4x4, TransformAndColor> PositionTextureInstanced { get; } =
        new(PositionTextureInstancedVert, PositionTextureTintedFrag,
            TextureVertex3D.ShaderVertexLayout,
            TransformAndColorVertexLayout,
            TransformLayout);
}
