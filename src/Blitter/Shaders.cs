using System.Numerics;

namespace Blitter;

/// <summary>
/// Built-in shaders for basic functionality.
/// </summary>
public static class Shaders
{
    #region Source and Args

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

    // Lit color shader (per-pixel Lambertian).
    //
    // Vertex stage: transforms position by Model then ViewProjection,
    // and forwards world-space position + world-space normal + the
    // baked vertex color to the fragment stage. Lighting is *not* done
    // at this stage, so it interpolates correctly across the triangle.
    //
    // Fragment stage: assembles the lit color from
    //   ambient + directional contribution + sum of point-light contributions
    // each modulated by the surface's per-vertex base color.
    //
    // Uniform layout:
    //   Vertex:
    //     b0 space1 = Model            (mat4)
    //     b1 space1 = ViewProjection   (mat4)
    //   Fragment:
    //     b0 space3 = AmbientLight     (rgba, alpha unused)
    //     b1 space3 = LightDirection   (xyz=dir TO directional light, world space; w unused)
    //     b2 space3 = LightColor       (directional light rgba, alpha unused)
    //     b3 space3 = PointLightCount  (.x = int count, rest reserved)
    //     t0 space2 = StructuredBuffer<PointLight>  (variable-length list)
    //
    // Normals are transformed by the upper-3x3 of the model matrix --
    // correct for rotation + uniform scale + translation. Non-uniform
    // scale would need a true inverse-transpose normal matrix; that's
    // a custom-shader job.
    private const string LitColorVertHlsl = """
        cbuffer Model : register(b0, space1) { float4x4 model;          };
        cbuffer VP    : register(b1, space1) { float4x4 viewProjection; };

        struct Input
        {
            float3 Position : TEXCOORD0;
            float3 Normal   : TEXCOORD1;
            float4 Color    : TEXCOORD2;
        };

        struct Output
        {
            float3 WorldPos  : TEXCOORD0;
            float3 WorldNorm : TEXCOORD1;
            float4 Color     : TEXCOORD2;
            float4 Position  : SV_Position;
        };

        Output main(Input input)
        {
            Output output;
            float4 worldPos    = mul(model, float4(input.Position, 1.0f));
            output.WorldPos    = worldPos.xyz;
            output.WorldNorm   = mul((float3x3)model, input.Normal);
            output.Color       = input.Color;
            output.Position    = mul(viewProjection, worldPos);
            return output;
        }
        """;

    // Matches the C# PointLight struct: 32 bytes per light, two vec4s.
    //   PositionRange.xyz   = world-space position
    //   PositionRange.w     = range (distance at which contribution is 0)
    //   ColorIntensity.rgb  = light color
    //   ColorIntensity.a    = intensity multiplier on top of color
    private const string LitColorFragHlsl = """
        struct PointLight
        {
            float4 PositionRange;
            float4 ColorIntensity;
        };

        StructuredBuffer<PointLight> pointLights : register(t0, space2);

        cbuffer Ambient    : register(b0, space3) { float4 ambient;         };
        cbuffer LightDir   : register(b1, space3) { float4 dirLightDir;     };
        cbuffer LightCol   : register(b2, space3) { float4 dirLightColor;   };
        cbuffer LightCount : register(b3, space3) { float4 pointLightCount; };

        struct Input
        {
            float3 WorldPos  : TEXCOORD0;
            float3 WorldNorm : TEXCOORD1;
            float4 Color     : TEXCOORD2;
        };

        float4 main(Input input) : SV_Target0
        {
            float3 N = normalize(input.WorldNorm);

            // Start with ambient.
            float3 lit = ambient.rgb;

            // Directional light contribution. Skip when direction is
            // zero (no directional configured) to avoid a NaN out of
            // normalize().
            float dirLen = length(dirLightDir.xyz);
            if (dirLen > 0.0001f)
            {
                float3 L = dirLightDir.xyz / dirLen;
                float NdotL = saturate(dot(N, L));
                lit += dirLightColor.rgb * NdotL;
            }

            // Sum all point light contributions.
            int count = (int)pointLightCount.x;
            for (int i = 0; i < count; i++)
            {
                PointLight pl = pointLights[i];
                float3 toLight = pl.PositionRange.xyz - input.WorldPos;
                float dist = length(toLight);
                float range = pl.PositionRange.w;
                if (range <= 0.0001f || dist >= range)
                    continue;
                float3 L = toLight / dist;
                float NdotL = saturate(dot(N, L));
                // Soft cutoff: full strength at the source, 0 exactly at range.
                float t = saturate(1.0f - dist / range);
                float atten = t * t;
                lit += pl.ColorIntensity.rgb * pl.ColorIntensity.a * NdotL * atten;
            }

            return float4(input.Color.rgb * lit, input.Color.a);
        }
        """;

    // Lit + textured shader. Same lighting math as LitColor; the base
    // surface color is `diffuseTexture(uv) * vertexColor` instead of
    // just the vertex color. This is the unified rendering path the
    // OBJ loader produces: a material's diffuse color goes into the
    // vertex tint slot, a 1x1 white texture stands in when no diffuse
    // map is supplied, so a single shader covers all four
    // {color × texture, no color × texture, color, plain} cases.
    //
    // Uniform layout: identical to LitColor. Vertex stage:
    //   b0 space1 = Model, b1 space1 = ViewProjection
    // Fragment stage:
    //   b0..b3 space3 = ambient/dirDir/dirColor/pointCount (matches LitColor)
    //   t0 space2 = sampled diffuse texture (Texture2D<float4>)
    //   s0 space2 = sampler for that texture
    //   t1 space2 = StructuredBuffer<PointLight>
    // The storage buffer slot bumps from t0 to t1 because SDL_shadercross
    // packs storage buffers AFTER sampled + storage textures within
    // space2 -- 1 sampled texture pushes the structured buffer to t1.
    private const string LitTextureVertHlsl = """
        cbuffer Model : register(b0, space1) { float4x4 model;          };
        cbuffer VP    : register(b1, space1) { float4x4 viewProjection; };

        struct Input
        {
            float3 Position : TEXCOORD0;
            float3 Normal   : TEXCOORD1;
            float2 TexCoord : TEXCOORD2;
            float4 Color    : TEXCOORD3;
        };

        struct Output
        {
            float3 WorldPos  : TEXCOORD0;
            float3 WorldNorm : TEXCOORD1;
            float2 TexCoord  : TEXCOORD2;
            float4 Color     : TEXCOORD3;
            float4 Position  : SV_Position;
        };

        Output main(Input input)
        {
            Output output;
            float4 worldPos    = mul(model, float4(input.Position, 1.0f));
            output.WorldPos    = worldPos.xyz;
            output.WorldNorm   = mul((float3x3)model, input.Normal);
            output.TexCoord    = input.TexCoord;
            output.Color       = input.Color;
            output.Position    = mul(viewProjection, worldPos);
            return output;
        }
        """;

    private const string LitTextureFragHlsl = """
        struct PointLight
        {
            float4 PositionRange;
            float4 ColorIntensity;
        };

        Texture2D<float4> diffuseTex : register(t0, space2);
        SamplerState      diffuseSmp : register(s0, space2);
        StructuredBuffer<PointLight> pointLights : register(t1, space2);

        cbuffer Ambient    : register(b0, space3) { float4 ambient;         };
        cbuffer LightDir   : register(b1, space3) { float4 dirLightDir;     };
        cbuffer LightCol   : register(b2, space3) { float4 dirLightColor;   };
        cbuffer LightCount : register(b3, space3) { float4 pointLightCount; };

        struct Input
        {
            float3 WorldPos  : TEXCOORD0;
            float3 WorldNorm : TEXCOORD1;
            float2 TexCoord  : TEXCOORD2;
            float4 Color     : TEXCOORD3;
        };

        float4 main(Input input) : SV_Target0
        {
            float3 N = normalize(input.WorldNorm);
            float4 baseColor = diffuseTex.Sample(diffuseSmp, input.TexCoord) * input.Color;

            float3 lit = ambient.rgb;

            float dirLen = length(dirLightDir.xyz);
            if (dirLen > 0.0001f)
            {
                float3 L = dirLightDir.xyz / dirLen;
                float NdotL = saturate(dot(N, L));
                lit += dirLightColor.rgb * NdotL;
            }

            int count = (int)pointLightCount.x;
            for (int i = 0; i < count; i++)
            {
                PointLight pl = pointLights[i];
                float3 toLight = pl.PositionRange.xyz - input.WorldPos;
                float dist = length(toLight);
                float range = pl.PositionRange.w;
                if (range <= 0.0001f || dist >= range)
                    continue;
                float3 L = toLight / dist;
                float NdotL = saturate(dot(N, L));
                float t = saturate(1.0f - dist / range);
                float atten = t * t;
                lit += pl.ColorIntensity.rgb * pl.ColorIntensity.a * NdotL * atten;
            }

            return float4(baseColor.rgb * lit, baseColor.a);
        }
        """;

    // ---- Instanced Shaders. Per-vertex inputs take the same TEXCOORDn
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

    // Instanced variant of LitTextureVertHlsl. Per-vertex inputs occupy
    // TEXCOORD0..3 (matching the non-instanced shader); per-instance
    // attributes follow on TEXCOORD4..8: a float4x4 transform consumes
    // four consecutive semantic indices (one per row) and is followed by
    // a float4 tint that multiplies the per-vertex color.
    //
    // The Model cbuffer is declared (so the shader matches LitArgs's
    // LightingArgsLayout exactly and we can reuse LitArgs as TArgs) but
    // unused -- the per-instance transform replaces it.
    private const string LitTextureInstancedVertHlsl = """
        cbuffer Model : register(b0, space1) { float4x4 model;          };
        cbuffer VP    : register(b1, space1) { float4x4 viewProjection; };

        struct Input
        {
            float3 Position            : TEXCOORD0;
            float3 Normal              : TEXCOORD1;
            float2 TexCoord            : TEXCOORD2;
            float4 Color               : TEXCOORD3;
            float4x4 InstanceTransform : TEXCOORD4;
            float4 InstanceTint        : TEXCOORD8;
        };

        struct Output
        {
            float3 WorldPos  : TEXCOORD0;
            float3 WorldNorm : TEXCOORD1;
            float2 TexCoord  : TEXCOORD2;
            float4 Color     : TEXCOORD3;
            float4 Position  : SV_Position;
        };

        Output main(Input input)
        {
            Output output;
            float4 worldPos    = mul(input.InstanceTransform, float4(input.Position, 1.0f));
            output.WorldPos    = worldPos.xyz;
            output.WorldNorm   = mul((float3x3)input.InstanceTransform, input.Normal);
            output.TexCoord    = input.TexCoord;
            output.Color       = input.Color * input.InstanceTint;
            output.Position    = mul(viewProjection, worldPos);
            return output;
        }
        """;

    // ---- Per-stage StageShader instances. Several sets share a fragment stage
    // (e.g. SolidColor is used by both PositionColor variants), so we cache
    // each stage shader as a singleton to keep GPU upload bookkeeping
    // identity-keyed and cheap.

    private static readonly VertexShader PositionColorVert =
        new(PositionColorVertHlsl);

    private static readonly VertexShader PositionColorWithTransformVert =
        new(PositionColorWithTransformVertHlsl);

    private static readonly VertexShader PositionTextureVert =
        new(PositionTextureVertHlsl);

    private static readonly VertexShader PositionTextureWithTransformVert =
        new(PositionTextureWithTransformVertHlsl);

    private static readonly FragmentShader SolidColorFrag =
        new(SolidColorFragHlsl);

    private static readonly FragmentShader PositionTextureFrag =
        new(PositionTextureFragHlsl);

    private static readonly VertexShader SkyboxVert =
        new(SkyboxVertHlsl);

    private static readonly FragmentShader SkyboxFrag =
        new(SkyboxFragHlsl);

    private static readonly VertexShader PositionVert =
        new(PositionVertHlsl);

    private static readonly VertexShader PositionWithTransformVert =
        new(PositionWithTransformVertHlsl);

    private static readonly FragmentShader SolidColorUniformFrag =
        new(SolidColorUniformFragHlsl);

    private static readonly FragmentShader WhiteFrag =
        new(WhiteFragHlsl);

    private static readonly VertexShader LitColorVert =
        new(LitColorVertHlsl);

    private static readonly FragmentShader LitColorFrag =
        new(LitColorFragHlsl);

    private static readonly VertexShader LitTextureVert =
        new(LitTextureVertHlsl);

    private static readonly FragmentShader LitTextureFrag =
        new(LitTextureFragHlsl);

    private static readonly VertexShader PositionInstancedVert =
        new(PositionInstancedVertHlsl);

    private static readonly VertexShader PositionColorInstancedVert =
        new(PositionColorInstancedVertHlsl);

    private static readonly VertexShader PositionTextureInstancedVert =
        new(PositionTextureInstancedVertHlsl);

    private static readonly VertexShader LitTextureInstancedVert =
        new(LitTextureInstancedVertHlsl);

    private static readonly FragmentShader PositionTextureTintedFrag =
        new(PositionTextureTintedFragHlsl);

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
    /// <see cref="TransformAndFColorArgs"/>.
    /// </summary>
    private static readonly ShaderArgsLayout TransformAndColorLayout = new(
        new ShaderArgElement(ShaderArgStage.Vertex,   0, ShaderArgKind.Matrix4x4),
        new ShaderArgElement(ShaderArgStage.Fragment, 0, ShaderArgKind.Float4));

    /// <summary>
    /// Six-slot layout matching <see cref="LitArgs"/>. Vertex stage gets
    /// the model and view-projection matrices; fragment stage gets the
    /// lighting fields (per-pixel Lambertian shading), including the
    /// point light count -- the actual point light data lives in a
    /// storage buffer the renderer binds separately.
    /// </summary>
    private static readonly ShaderArgsLayout LightingArgsLayout = new(
        new ShaderArgElement(ShaderArgStage.Vertex,   0, ShaderArgKind.Matrix4x4), // Model
        new ShaderArgElement(ShaderArgStage.Vertex,   1, ShaderArgKind.Matrix4x4), // ViewProjection
        new ShaderArgElement(ShaderArgStage.Fragment, 0, ShaderArgKind.Float4),    // AmbientLight
        new ShaderArgElement(ShaderArgStage.Fragment, 1, ShaderArgKind.Float4),    // LightDirection
        new ShaderArgElement(ShaderArgStage.Fragment, 2, ShaderArgKind.Float4),    // LightColor
        new ShaderArgElement(ShaderArgStage.Fragment, 3, ShaderArgKind.Float4));   // PointLightCount
    #endregion

    /// <summary>
    /// Position-only vertices, no transform; emits opaque white. Positions
    /// must already be in normalized device coordinates.
    /// </summary>
    public static Shader<Vertex3D> Position { get; } =
        new(PositionVert, WhiteFrag, Vertex3D.ShaderVertexLayout, ShaderTextureLayout.Empty);

    /// <summary>
    /// Position-only vertices, transformed by a per-draw 4x4 matrix; emits
    /// opaque white.
    /// </summary>
    public static Shader<Vertex3D, TransformArgs> PositionWithTransform { get; } =
        new(PositionWithTransformVert, WhiteFrag, Vertex3D.ShaderVertexLayout, TransformLayout, ShaderTextureLayout.Empty);

    /// <summary>
    /// Position-only vertices, transformed by a per-draw 4x4 matrix, with a
    /// per-draw fragment color. Pair with
    /// <see cref="TransformAndFColorArgs"/>.
    /// </summary>
    public static Shader<Vertex3D, TransformAndFColorArgs> PositionWithTransformAndColor { get; } =
        new(PositionWithTransformVert, SolidColorUniformFrag, Vertex3D.ShaderVertexLayout, TransformAndColorLayout, ShaderTextureLayout.Empty);

    /// <summary>
    /// Draws each vertex at its position with its baked color, with no
    /// transformation. Positions must already be in normalized device
    /// coordinates (the visible cube is -1 to 1 on each axis). Useful for
    /// screen-space drawing or testing.
    /// </summary>
    public static Shader<ColorVertex3D> PositionColor { get; } =
        new(PositionColorVert, SolidColorFrag, ColorVertex3D.ShaderVertexLayout, ShaderTextureLayout.Empty);

    /// <summary>
    /// Draws each vertex at its position with its color, transforming the
    /// position by a per-draw 4x4 model-view-projection matrix.
    /// </summary>
    /// <remarks>
    /// The args type is <see cref="TransformArgs"/> (a single
    /// <see cref="Matrix4x4"/> field) rather than a bare
    /// <see cref="Matrix4x4"/> so this shader works with
    /// <see cref="Renderer3D.DrawMesh{TVertex,TArgs}(Mesh{TVertex},
    /// Shader{TVertex,TArgs}, in TArgs)"/>: pass a model matrix and
    /// the renderer composes <see cref="Renderer3D.Camera"/> into it.
    /// Existing callers passing <c>model * viewProjection</c> continue
    /// to work via the implicit <see cref="Matrix4x4"/> → <see
    /// cref="TransformArgs"/> conversion.
    /// </remarks>
    public static Shader<ColorVertex3D, TransformArgs> PositionColorWithTransform { get; } =
        new(PositionColorWithTransformVert, SolidColorFrag, ColorVertex3D.ShaderVertexLayout, TransformLayout, ShaderTextureLayout.Empty);

    /// <summary>
    /// Draws each vertex at its position, sampling the bound texture using
    /// the vertex texture coordinate, with no transformation. Positions must
    /// already be in normalized device coordinates.
    /// </summary>
    public static Shader<TextureVertex3D> PositionTexture { get; } =
        new(PositionTextureVert, PositionTextureFrag, TextureVertex3D.ShaderVertexLayout, ShaderTextureLayout.SingleTexture2D);

    /// <summary>
    /// Draws each vertex at its position transformed by a per-draw 4x4
    /// model-view-projection matrix, sampling the bound texture using the
    /// vertex texture coordinate.
    /// </summary>
    public static Shader<TextureVertex3D, TransformArgs> PositionTextureWithTransform { get; } =
        new(PositionTextureWithTransformVert, PositionTextureFrag, TextureVertex3D.ShaderVertexLayout, TransformLayout, ShaderTextureLayout.SingleTexture2D);

    /// <summary>
    /// Lit color shader: per-pixel Lambertian shading from the renderer's
    /// <see cref="Renderer3D.AmbientLight"/>,
    /// <see cref="Renderer3D.DirectionalLight"/>, and every entry in
    /// <see cref="Renderer3D.PointLights"/>, modulated by the per-vertex
    /// baked color. Pair with <see cref="LitVertex3D"/> and
    /// <see cref="LitArgs"/>: callers supply <see cref="LitArgs.Model"/>;
    /// the renderer fills in the view-projection, lighting fields, and
    /// point-light buffer through <see cref="IUniformArgs{TSelf}"/>.
    /// </summary>
    /// <remarks>
    /// Normals are transformed by the model matrix's upper-3x3 -- correct
    /// for rotation + uniform scale + translation. Non-uniform scales need
    /// a custom shader using a true inverse-transpose normal matrix.
    /// </remarks>
    public static Shader<LitVertex3D, LitArgs> LitColor { get; } =
        new(LitColorVert, LitColorFrag, LitVertex3D.ShaderVertexLayout, LightingArgsLayout, ShaderTextureLayout.Empty);

    /// <summary>
    /// Lit + textured shader: per-pixel Lambertian shading using the
    /// renderer's <see cref="Renderer3D.AmbientLight"/>,
    /// <see cref="Renderer3D.DirectionalLight"/>, and every entry in
    /// <see cref="Renderer3D.PointLights"/>, applied to a base color of
    /// <c>diffuseTexture(uv) * vertexColor</c>. Pair with
    /// <see cref="LitTextureVertex3D"/> and <see cref="LitArgs"/>; bind
    /// the diffuse texture through the standard textured
    /// <c>DrawMesh(mesh, texture, shader, args)</c> overload.
    /// </summary>
    /// <remarks>
    /// The arg layout is identical to <see cref="LitColor"/>, so any
    /// caller that already uses LitColor can reuse the same args struct.
    /// Setting the vertex tint to white falls back to "texture only";
    /// supplying a 1x1 white texture falls back to "vertex tint only";
    /// both white = unlit-looking flat white surface lit only by ambient.
    /// </remarks>
    public static Shader<LitTextureVertex3D, LitArgs> LitTexture { get; } =
        new(LitTextureVert, LitTextureFrag, LitTextureVertex3D.ShaderVertexLayout, LightingArgsLayout, ShaderTextureLayout.SingleTexture2D);

    /// <summary>
    /// Skybox shader: samples the bound <see cref="Cubemap"/> by the
    /// world-space direction implied by each vertex's position, with
    /// depth forced to the far plane so the result always renders
    /// behind everything else. Pair with a unit cube mesh whose
    /// vertices span [-1, 1] on each axis (e.g. the cube from
    /// <c>samples/IndexedCube.cs</c>) and pass
    /// <see cref="Camera.GetSkyboxViewProjection(float)"/> as the
    /// per-draw transform so the skybox follows the camera. Use
    /// <see cref="DepthMode.Solid"/> -- the shader's depth output of
    /// 1.0 ensures the skybox draws behind opaque geometry without
    /// special depth state.
    /// </summary>
    public static Shader<Vertex3D, Matrix4x4> Skybox { get; } =
        new(SkyboxVert, SkyboxFrag, Vertex3D.ShaderVertexLayout, TransformLayout, ShaderTextureLayout.SingleTextureCube);

    /// <summary>
    /// The per-instance vertex layout used by every built-in
    /// <c>*Instanced</c> shader: a 4x4 transform followed by an 8-bit
    /// RGBA color. Pairs with <see cref="TransformAndColorInstance"/>.
    /// </summary>
    private static readonly ShaderVertexLayout TransformAndColorVertexLayout = new(
        ShaderVertexElementKind.Matrix4x4,
        ShaderVertexElementKind.Color4);

    /// <summary>
    /// Instanced variant of <see cref="PositionWithTransform"/>: draws the
    /// mesh once per <see cref="TransformAndColorInstance"/> in the supplied span.
    /// The per-call uniform is the camera view-projection matrix; each
    /// instance contributes its own world transform and color.
    /// </summary>
    public static Shader<Vertex3D, Matrix4x4, TransformAndColorInstance> PositionInstanced { get; } =
        new(PositionInstancedVert, SolidColorFrag,
            Vertex3D.ShaderVertexLayout,
            TransformAndColorVertexLayout,
            TransformLayout,
            ShaderTextureLayout.Empty);

    /// <summary>
    /// Instanced variant of <see cref="PositionColorWithTransform"/>: the
    /// per-vertex color is multiplied by the per-instance color, so a
    /// white-color instance shows the mesh's baked colors unchanged.
    /// </summary>
    public static Shader<ColorVertex3D, Matrix4x4, TransformAndColorInstance> PositionColorInstanced { get; } =
        new(PositionColorInstancedVert, SolidColorFrag,
            ColorVertex3D.ShaderVertexLayout,
            TransformAndColorVertexLayout,
            TransformLayout,
            ShaderTextureLayout.Empty);

    /// <summary>
    /// Instanced variant of <see cref="PositionTextureWithTransform"/>:
    /// the texture sample is multiplied by the per-instance color.
    /// </summary>
    public static Shader<TextureVertex3D, Matrix4x4, TransformAndColorInstance> PositionTextureInstanced { get; } =
        new(PositionTextureInstancedVert, PositionTextureTintedFrag,
            TextureVertex3D.ShaderVertexLayout,
            TransformAndColorVertexLayout,
            TransformLayout,
            ShaderTextureLayout.SingleTexture2D);

    /// <summary>
    /// Instanced variant of <see cref="LitTexture"/>: the mesh is drawn
    /// once per <see cref="TransformAndColorInstance"/> in the supplied
    /// span. Each instance contributes its own world transform and a
    /// tint that is multiplied with the per-vertex color before
    /// per-pixel Lambertian shading. Camera view-projection, ambient,
    /// directional, and point lights are composed from the renderer
    /// just like the non-instanced shader.
    /// </summary>
    /// <remarks>
    /// Reuses the <see cref="LitArgs"/> args layout exactly: the
    /// <see cref="LitArgs.Model"/> field is unused (per-instance
    /// transform replaces it) and can be left at default. The
    /// per-instance transform's upper 3x3 is used to transform normals,
    /// matching <see cref="LitTexture"/>'s rotation + uniform scale +
    /// translation assumption.
    /// </remarks>
    public static Shader<LitTextureVertex3D, LitArgs, TransformAndColorInstance> LitTextureInstanced { get; } =
        new(LitTextureInstancedVert, LitTextureFrag,
            LitTextureVertex3D.ShaderVertexLayout,
            TransformAndColorVertexLayout,
            LightingArgsLayout,
            ShaderTextureLayout.SingleTexture2D);
}
