namespace Blitter.Bits;

/// <summary>
/// Built-in physically based shaders. The available shader pairs with
/// <see cref="PbrMaterial"/> via <see cref="StandardMaterializer"/>;
/// callers normally reach the shader through
/// <c>Renderer3D.DrawMesh(mesh, material, ...)</c> rather than binding it
/// directly, but the property is exposed for advanced custom dispatchers.
/// </summary>
public static class PbrShaders
{
    #region HLSL sources

    // Vertex stage: identical wire to LitTextureVertHlsl -- transform
    // position by model, push world-space position and a model-rotated
    // normal to the fragment stage. Kept separate so PBR can grow its
    // own vertex layout later (tangents) without touching the lit-flat
    // path.
    private const string LitPbrVertHlsl = """
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

    // Cook-Torrance microfacet BRDF: GGX (Trowbridge-Reitz) NDF, Smith-GGX
    // geometry term (height-correlated), Schlick Fresnel. Diffuse is
    // Lambert weighted by (1 - F) * (1 - metallic). Image-based
    // lighting (IBL) uses Karis split-sum: a diffuse environment cube
    // sampled by N, plus a roughness-LOD-encoded specular environment
    // cube sampled by R combined with the split-sum BRDF 2D LUT.
    //
    // Sampled textures occupy t0..t6 in space2 (4 user PBR + 3 IBL),
    // matching slots 0..6 of PbrTextureLayout. SDL_GPU packs storage
    // buffers AFTER sampled textures within space2, so the point-light
    // structured buffer lands at t7.
    private const string LitPbrFragHlsl = """
        struct PointLight
        {
            float4 PositionRange;
            float4 ColorIntensity;
        };

        Texture2D<float4> baseColorTex : register(t0, space2);
        SamplerState      baseColorSmp : register(s0, space2);
        Texture2D<float4> mrTex        : register(t1, space2);
        SamplerState      mrSmp        : register(s1, space2);
        Texture2D<float4> occTex       : register(t2, space2);
        SamplerState      occSmp       : register(s2, space2);
        Texture2D<float4> emissiveTex  : register(t3, space2);
        SamplerState      emissiveSmp  : register(s3, space2);
        TextureCube<float4> diffuseTex     : register(t4, space2);
        SamplerState        diffuseSmp     : register(s4, space2);
        TextureCube<float4> specularTex    : register(t5, space2);
        SamplerState        specularSmp    : register(s5, space2);
        Texture2D<float4>   brdfLutTex     : register(t6, space2);
        SamplerState        brdfLutSmp     : register(s6, space2);
        StructuredBuffer<PointLight> pointLights : register(t7, space2);

        cbuffer Material : register(b0, space3)
        {
            float4 baseColorFactor;     // rgba
            float4 matFactors;          // x metallic, y roughness, z occlusion, w specularMaxMip
            float4 emissiveFactor;      // rgb, w _
            float4 cameraPosition;      // xyz world-space camera, w _
        };
        cbuffer Ambient    : register(b1, space3) { float4 ambient;       };
        cbuffer LightDir   : register(b2, space3) { float4 dirLightDir;   }; // xyz dir, w point-light count
        cbuffer LightCol   : register(b3, space3) { float4 dirLightColor; };

        struct Input
        {
            float3 WorldPos  : TEXCOORD0;
            float3 WorldNorm : TEXCOORD1;
            float2 TexCoord  : TEXCOORD2;
            float4 Color     : TEXCOORD3;
        };

        static const float PI = 3.14159265f;

        float D_GGX(float NdotH, float a2)
        {
            float d = (NdotH * NdotH) * (a2 - 1.0f) + 1.0f;
            return a2 / max(PI * d * d, 1e-5f);
        }

        float V_SmithGGXCorrelated(float NdotV, float NdotL, float a2)
        {
            // Height-correlated Smith visibility, with the 1/(4*NdotV*NdotL)
            // factor folded in so the BRDF reduces to D * F * V.
            float ggxV = NdotL * sqrt(NdotV * NdotV * (1.0f - a2) + a2);
            float ggxL = NdotV * sqrt(NdotL * NdotL * (1.0f - a2) + a2);
            return 0.5f / max(ggxV + ggxL, 1e-5f);
        }

        float3 F_Schlick(float VdotH, float3 F0)
        {
            float f = pow(saturate(1.0f - VdotH), 5.0f);
            return F0 + (1.0f - F0) * f;
        }

        // Roughness-aware Schlick Fresnel for IBL ambient. As roughness
        // climbs the grazing Fresnel pulls toward (1-roughness) instead
        // of pure white so rough metals don't get an unphysically bright
        // edge highlight from the environment.
        float3 F_SchlickRoughness(float cosTheta, float3 F0, float roughness)
        {
            float f = pow(saturate(1.0f - cosTheta), 5.0f);
            return F0 + (max(float3(1.0f - roughness, 1.0f - roughness, 1.0f - roughness), F0) - F0) * f;
        }

        float3 BRDF(float3 N, float3 V, float3 L, float3 albedo, float metallic, float roughness)
        {
            float3 H = normalize(V + L);
            float NdotL = saturate(dot(N, L));
            float NdotV = saturate(dot(N, V));
            float NdotH = saturate(dot(N, H));
            float VdotH = saturate(dot(V, H));

            // Perceptual -> linear roughness.
            float a  = max(roughness * roughness, 1e-3f);
            float a2 = a * a;

            // F0: 0.04 for dielectrics, albedo for metals.
            float3 F0 = lerp(float3(0.04f, 0.04f, 0.04f), albedo, metallic);

            float  D = D_GGX(NdotH, a2);
            float  V_ = V_SmithGGXCorrelated(NdotV, NdotL, a2);
            float3 F = F_Schlick(VdotH, F0);

            // PI factors are dropped from both specular and Lambert so
            // intensities match the rest of the engine's lit shaders
            // (LitColor / LitTexture), which use engine-Lambert with no
            // 1/PI. Net effect on relative shading is identical.
            float3 specular = D * V_ * F * PI;
            float3 kd = (1.0f - F) * (1.0f - metallic);
            float3 diffuse = kd * albedo;

            return (diffuse + specular) * NdotL;
        }

        float4 main(Input input) : SV_Target0
        {
            float3 N = normalize(input.WorldNorm);

            // -- Material samples
            float4 baseSample = baseColorTex.Sample(baseColorSmp, input.TexCoord) * baseColorFactor * input.Color;
            float3 albedo = baseSample.rgb;
            float  alpha  = baseSample.a;

            float4 mr = mrTex.Sample(mrSmp, input.TexCoord);
            float  metallic  = saturate(mr.b * matFactors.x);
            float  roughness = saturate(mr.g * matFactors.y);

            float aoSample = occTex.Sample(occSmp, input.TexCoord).r;
            float ao = lerp(1.0f, aoSample, saturate(matFactors.z));

            float3 emissive = emissiveTex.Sample(emissiveSmp, input.TexCoord).rgb * emissiveFactor.rgb;

            // View vector: world-space camera minus pixel position.
            float3 V = normalize(cameraPosition.xyz - input.WorldPos);
            float NdotV = saturate(dot(N, V));

            // -- Image-based lighting (Karis split-sum with
            // Fdez-Aguera 2019 multi-scattering compensation).
            float3 F0  = lerp(float3(0.04f, 0.04f, 0.04f), albedo, metallic);
            float3 Fr  = F_SchlickRoughness(NdotV, F0, roughness);
            // Environment yaw (radians) packed into emissiveFactor.w
            // by the materializer. Rotates cubemap sample directions
            // around Y without rebaking; horizon/zenith are symmetric
            // around Y so only the sun's azimuth visibly moves.
            float yaw = emissiveFactor.w;
            float yawCos = cos(yaw);
            float yawSin = sin(yaw);
            float3 Nenv  = float3(yawCos * N.x + yawSin * N.z, N.y, -yawSin * N.x + yawCos * N.z);

            float3 irr = diffuseTex.Sample(diffuseSmp, Nenv).rgb;
            float3 R   = reflect(-V, N);
            float3 Renv = float3(yawCos * R.x + yawSin * R.z, R.y, -yawSin * R.x + yawCos * R.z);
            float  maxMip = matFactors.w;
            float3 pref   = specularTex.SampleLevel(specularSmp, Renv, roughness * maxMip).rgb;
            // Karis 2014 analytic LUT approximation. The baked LUT
            // texture produced by GGX importance sampling collapses
            // to zero along the grazing edge (low-roughness samples
            // all cluster at H=N, where the reflected direction is
            // tangent and NdotL=0), which causes IBL to vanish at
            // sphere silhouettes. This closed form gives correct
            // grazing values with no sampling bias.
            float4 c0 = float4(-1.0f, -0.0275f, -0.572f, 0.022f);
            float4 c1 = float4( 1.0f,  0.0425f,  1.040f, -0.040f);
            float4 r4 = roughness * c0 + c1;
            float a004 = min(r4.x * r4.x, exp2(-9.28f * NdotV)) * r4.x + r4.y;
            float2 brdf = float2(-1.04f, 1.04f) * a004 + r4.zw;

            // Fdez-Aguera 2019 multi-scattering compensation. Plain
            // Karis split-sum loses energy at grazing (Smith G drops
            // to zero), darkening rims and concentrating IBL into a
            // bright spot at NdotV=1. The Ems term adds the missing
            // energy back, redistributing it between specular and
            // diffuse so a uniform environment yields a flat-shaded
            // sphere instead of a bright dot on a dark ball.
            float3 FssEss = Fr * brdf.x + brdf.y;
            float  Ems    = 1.0f - (brdf.x + brdf.y);
            float3 Favg   = F0 + (1.0f - F0) / 21.0f;
            float3 FmsEms = Ems * FssEss * Favg / (1.0f - Favg * Ems);
            float3 kdMs   = albedo * (1.0f - FssEss - FmsEms) * (1.0f - metallic);
            float3 iblDiffuse  = irr * kdMs;
            float3 iblSpecular = pref * FssEss + irr * FmsEms;
            // Tinted by the renderer's ambient color so authors can dim
            // or warm the IBL contribution per-scene without rebaking
            // cubemaps.
            float3 lit = (iblDiffuse + iblSpecular) * ao * ambient.rgb;

            // -- Directional light
            float dirLen = length(dirLightDir.xyz);
            if (dirLen > 0.0001f)
            {
                float3 L = dirLightDir.xyz / dirLen; // points FROM surface TO light
                lit += BRDF(N, V, L, albedo, metallic, roughness) * dirLightColor.rgb;
            }

            // -- Point lights. Count is packed into dirLightDir.w to
            // keep the shader within SDL_GPU's 4 fragment cbuffers.
            int count = (int)dirLightDir.w;
            for (int i = 0; i < count; i++)
            {
                PointLight pl = pointLights[i];
                float3 toLight = pl.PositionRange.xyz - input.WorldPos;
                float dist = length(toLight);
                float range = pl.PositionRange.w;
                if (range <= 0.0001f || dist >= range)
                    continue;
                float3 L = toLight / dist;
                float t = saturate(1.0f - dist / range);
                float atten = t * t;
                lit += BRDF(N, V, L, albedo, metallic, roughness)
                     * pl.ColorIntensity.rgb * pl.ColorIntensity.a * atten;
            }

            return float4(lit + emissive, alpha);
        }
        """;

    #endregion

    private static readonly VertexShader LitPbrVert = new(LitPbrVertHlsl);
    private static readonly FragmentShader LitPbrFrag = new(LitPbrFragHlsl);

    // Vertex: Model, ViewProjection (b0, b1 space1).
    // Fragment: Material (64-byte block at b0 space3, pushed as one
    // Matrix4x4 covering BaseColorFactor + MaterialFactors +
    // EmissiveFactor + MaterialReserved), then Ambient (b1),
    // LightDirection (b2, w = point-light count), LightColor (b3).
    // Order matches PbrArgs field layout.
    private static readonly ShaderArgsLayout PbrArgsLayout = new(
        new ShaderArgElement(ShaderArgStage.Vertex,   0, ShaderArgKind.Matrix4x4),
        new ShaderArgElement(ShaderArgStage.Vertex,   1, ShaderArgKind.Matrix4x4),
        new ShaderArgElement(ShaderArgStage.Fragment, 0, ShaderArgKind.Matrix4x4), // Material block
        new ShaderArgElement(ShaderArgStage.Fragment, 1, ShaderArgKind.Float4),    // Ambient
        new ShaderArgElement(ShaderArgStage.Fragment, 2, ShaderArgKind.Float4),    // LightDirection (w = point-light count)
        new ShaderArgElement(ShaderArgStage.Fragment, 3, ShaderArgKind.Float4));   // LightColor

    internal static readonly ShaderTextureLayout PbrTextureLayout = new(
        new ShaderTextureSlot("baseColor",         ShaderTextureDimension.Texture2D),
        new ShaderTextureSlot("metallicRoughness", ShaderTextureDimension.Texture2D),
        new ShaderTextureSlot("occlusion",         ShaderTextureDimension.Texture2D),
        new ShaderTextureSlot("emissive",          ShaderTextureDimension.Texture2D),
        new ShaderTextureSlot("diffuse",           ShaderTextureDimension.TextureCube),
        new ShaderTextureSlot("specular",          ShaderTextureDimension.TextureCube),
        new ShaderTextureSlot("specularLut",       ShaderTextureDimension.Texture2D));

    /// <summary>
    /// Cook-Torrance PBR shader (GGX + Smith + Schlick) on
    /// <see cref="LitTextureVertex3D"/> with <see cref="PbrArgs"/>.
    /// Reuses the renderer's directional + point-light pipeline.
    /// </summary>
    /// <remarks>
    /// Expects four fragment textures bound in order, following the
    /// glTF 2.0 channel layout: base color (RGBA), metallic-roughness
    /// (G = roughness, B = metallic), ambient occlusion (R), and
    /// emissive (RGB).
    /// </remarks>
    public static Shader<LitTextureVertex3D, PbrArgs> LitPbr { get; } =
        new(LitPbrVert, LitPbrFrag,
            LitTextureVertex3D.ShaderVertexLayout,
            PbrArgsLayout,
            PbrTextureLayout);
}
