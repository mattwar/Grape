using System.Numerics;
using System.Runtime.InteropServices;

namespace Blitter.Bits;

/// <summary>
/// Generates cubemaps for image-based lighting using the GPU and custom shaders.
/// </summary>
public static class EnvironmentBaker
{
    /// <summary>
    /// Generates a prefiltered specular cubemap from a <see cref="CubeTexture"/> source, on the GPU. 
    /// </summary>
    /// <param name="source">Source environment cube (HDR sky, etc.).</param>
    /// <param name="faceSize">Edge length per face of the destination.</param>
    /// <param name="levels">Mip-level count; <c>null</c> picks the full chain.</param>
    /// <param name="samples">GGX importance samples per pixel per face.</param>
    public static GpuCubemap BakePrefilteredSpecular(
        CubeTexture source,
        int faceSize = 128,
        int? levels = null,
        int samples = 256)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(faceSize);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(samples);

        int levelCount = levels ?? ComputeFullMipLevels(faceSize);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(levelCount);

        var dest = GpuCubemap.Create(faceSize, levels: levelCount, renderTarget: true);
        var device = GpuDevice.Default;
        var mesh = s_fullscreenTri.Value;
        var shader = s_prefilterShader.Value;

        foreach (var face in CubeFaceExtensions.All)
        {
            var forward = face.GetForward();
            var up = face.GetUp();
            // Right = up x forward gives the D3D/Vulkan cube-face image
            // orientation: top of the face image is +up, right of the
            // face image is the resulting cross product.
            var right = Vector3.Normalize(Vector3.Cross(up, forward));

            for (int mip = 0; mip < levelCount; mip++)
            {
                float roughness = levelCount == 1
                    ? 0f
                    : mip / (float)(levelCount - 1);
                var args = new PrefilterArgs
                {
                    RightAndRoughness = new Vector4(right, roughness),
                    UpAndSamples = new Vector4(up, samples),
                    Forward = new Vector4(forward, 0f),
                };

                using var renderer = new GpuCubemapFaceRenderer(
                    device, dest, face, mip, useDepth: false);
                renderer.AutoClear = false;
                renderer.DrawMeshRaw(mesh, source, shader, in args);
                renderer.Render();
            }
        }

        return dest;
    }

    /// <summary>
    /// Generates a diffuse irradiance cubemap from a source <see cref="CubeTexture"/>.
    /// </summary>
    /// <param name="source">Source environment cube.</param>
    /// <param name="faceSize">Edge length per face of the destination. 32 is plenty -- the integrand is very smooth.</param>
    /// <param name="samples">Cosine-weighted hemisphere samples per pixel.</param>
    public static GpuCubemap BakeIrradiance(
        CubeTexture source,
        int faceSize = 32,
        int samples = 256)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(faceSize);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(samples);

        var dest = GpuCubemap.Create(faceSize, levels: 1, renderTarget: true);
        var device = GpuDevice.Default;
        var mesh = s_fullscreenTri.Value;
        var shader = s_irradianceShader.Value;

        foreach (var face in CubeFaceExtensions.All)
        {
            var forward = face.GetForward();
            var up = face.GetUp();
            var right = Vector3.Normalize(Vector3.Cross(up, forward));

            var args = new IrradianceArgs
            {
                Right = new Vector4(right, 0f),
                UpAndSamples = new Vector4(up, samples),
                Forward = new Vector4(forward, 0f),
            };

            using var renderer = new GpuCubemapFaceRenderer(
                device, dest, face, mip: 0, useDepth: false);
            renderer.AutoClear = false;
            renderer.DrawMeshRaw(mesh, source, shader, in args);
            renderer.Render();
        }

        return dest;
    }

    /// <summary>
    /// Generates a specular LUT (look up table) on the GPU and
    /// downloads it to a CPU <see cref="Bitmap"/>. Returned as a CPU
    /// image so it can be consumed by both 2D and 3D renderers; the
    /// GPU still does all the integration work.
    /// </summary>
    /// <param name="size">Edge length of the square LUT.</param>
    /// <param name="samples">Importance samples per texel.</param>
    public static Bitmap BakeSpecularLut(int size = 256, int samples = 512)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(samples);

        var dest = Bitmap.Create(size, size, PixelFormat.ABGR8888);
        var mesh = s_fullscreenTri.Value;
        var shader = s_specularLutShader.Value;
        var args = new SpecularLutArgs { Params = new Vector4(samples, 0f, 0f, 0f) };

        dest.Render3D(rd => rd.DrawMeshRaw(mesh, shader, in args));
        return dest;
    }

    // floor(log2(size)) + 1 — full chain down to 1x1.
    private static int ComputeFullMipLevels(int size) =>
        Math.Max(1, (int)Math.Floor(Math.Log2(size)) + 1);

    // Three Vector4 cbuffer slots, 48 bytes total. Packs scalar params
    // into the .w of vectors that have a spare component.
    [StructLayout(LayoutKind.Sequential)]
    private struct PrefilterArgs
    {
        public Vector4 RightAndRoughness;   // xyz = face right,   w = roughness
        public Vector4 UpAndSamples;        // xyz = face up,      w = sample count
        public Vector4 Forward;             // xyz = face forward, w = unused
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IrradianceArgs
    {
        public Vector4 Right;               // xyz = face right,   w = unused
        public Vector4 UpAndSamples;        // xyz = face up,      w = sample count
        public Vector4 Forward;             // xyz = face forward, w = unused
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SpecularLutArgs
    {
        public Vector4 Params;              // x = sample count
    }

    // Single fullscreen triangle in clip space. The vertex shader passes
    // positions through unchanged; the fragment uses the interpolated
    // xy as NDC to reconstruct the per-pixel view direction.
    private static readonly Lazy<Mesh<Vertex3D>> s_fullscreenTri = new(() =>
        Mesh.Create<Vertex3D>(new ReadOnlySpan<Vertex3D>(new[]
        {
            new Vertex3D(-1f, -1f, 0f),
            new Vertex3D( 3f, -1f, 0f),
            new Vertex3D(-1f,  3f, 0f),
        })));

    private static readonly Lazy<Shader<Vertex3D, PrefilterArgs>> s_prefilterShader = new(() =>
        new Shader<Vertex3D, PrefilterArgs>(
            new VertexShader(VertexHlsl),
            new FragmentShader(PrefilterFragmentHlsl),
            Vertex3D.ShaderVertexLayout,
            new ShaderArgsLayout(
                new ShaderArgElement(ShaderArgStage.Fragment, 0, ShaderArgKind.Float4),
                new ShaderArgElement(ShaderArgStage.Fragment, 1, ShaderArgKind.Float4),
                new ShaderArgElement(ShaderArgStage.Fragment, 2, ShaderArgKind.Float4)),
            ShaderTextureLayout.SingleTextureCube));

    private static readonly Lazy<Shader<Vertex3D, IrradianceArgs>> s_irradianceShader = new(() =>
        new Shader<Vertex3D, IrradianceArgs>(
            new VertexShader(VertexHlsl),
            new FragmentShader(IrradianceFragmentHlsl),
            Vertex3D.ShaderVertexLayout,
            new ShaderArgsLayout(
                new ShaderArgElement(ShaderArgStage.Fragment, 0, ShaderArgKind.Float4),
                new ShaderArgElement(ShaderArgStage.Fragment, 1, ShaderArgKind.Float4),
                new ShaderArgElement(ShaderArgStage.Fragment, 2, ShaderArgKind.Float4)),
            ShaderTextureLayout.SingleTextureCube));

    private static readonly Lazy<Shader<Vertex3D, SpecularLutArgs>> s_specularLutShader = new(() =>
        new Shader<Vertex3D, SpecularLutArgs>(
            new VertexShader(VertexHlsl),
            new FragmentShader(SpecularLutFragmentHlsl),
            Vertex3D.ShaderVertexLayout,
            new ShaderArgsLayout(
                new ShaderArgElement(ShaderArgStage.Fragment, 0, ShaderArgKind.Float4)),
            ShaderTextureLayout.Empty));

    private const string VertexHlsl = """
        struct Input  { float3 Position : TEXCOORD0; };
        struct Output { float2 Ndc : TEXCOORD0; float4 Position : SV_Position; };

        Output main(Input input)
        {
            Output o;
            o.Ndc = input.Position.xy;
            o.Position = float4(input.Position, 1.0f);
            return o;
        }
        """;

    private const string PrefilterFragmentHlsl = """
        cbuffer RightR  : register(b0, space3) { float4 RightR;  }; // xyz=right,   w=roughness
        cbuffer UpN     : register(b1, space3) { float4 UpN;     }; // xyz=up,      w=sampleCount
        cbuffer Forward : register(b2, space3) { float4 Forward; }; // xyz=forward, w=unused

        TextureCube<float4> srcTex : register(t0, space2);
        SamplerState        srcSmp : register(s0, space2);

        struct Input { float2 Ndc : TEXCOORD0; };

        static const float PI = 3.14159265f;

        // Van der Corput radical inverse in base 2 via bit reversal.
        float RadicalInverseVdC(uint bits)
        {
            bits = (bits << 16u) | (bits >> 16u);
            bits = ((bits & 0x55555555u) << 1u) | ((bits & 0xAAAAAAAAu) >> 1u);
            bits = ((bits & 0x33333333u) << 2u) | ((bits & 0xCCCCCCCCu) >> 2u);
            bits = ((bits & 0x0F0F0F0Fu) << 4u) | ((bits & 0xF0F0F0F0u) >> 4u);
            bits = ((bits & 0x00FF00FFu) << 8u) | ((bits & 0xFF00FF00u) >> 8u);
            return float(bits) * 2.3283064365386963e-10f;
        }

        float2 Hammersley(uint i, uint n)
        {
            return float2(float(i) / float(n), RadicalInverseVdC(i));
        }

        // GGX importance-sampled half-vector around N, in world space.
        float3 ImportanceSampleGGX(float2 xi, float3 N, float a2)
        {
            float phi      = 2.0f * PI * xi.x;
            float cosTheta = sqrt((1.0f - xi.y) / (1.0f + (a2 - 1.0f) * xi.y));
            float sinTheta = sqrt(max(0.0f, 1.0f - cosTheta * cosTheta));

            float3 hLocal = float3(cos(phi) * sinTheta,
                                   sin(phi) * sinTheta,
                                   cosTheta);

            // Tangent frame around N -- pick the world axis least
            // aligned with N for numerical stability.
            float3 worldUp = abs(N.y) < 0.999f ? float3(0, 1, 0) : float3(1, 0, 0);
            float3 T = normalize(cross(worldUp, N));
            float3 B = cross(N, T);
            return normalize(T * hLocal.x + B * hLocal.y + N * hLocal.z);
        }

        float4 main(Input input) : SV_Target0
        {
            float roughness   = RightR.w;
            uint  sampleCount = (uint)UpN.w;

            // Split-sum simplification: assume V == R == N. The per-pixel
            // N is reconstructed from the face orientation and the
            // interpolated NDC.
            float3 N = normalize(Forward.xyz
                               + input.Ndc.x * RightR.xyz
                               + input.Ndc.y * UpN.xyz);
            float3 V = N;

            // Roughness 0 degenerates to a delta lobe -- sample the
            // source directly to avoid noise.
            if (roughness < 1e-4f)
                return float4(srcTex.SampleLevel(srcSmp, N, 0).rgb, 1.0f);

            float a  = roughness * roughness;
            float a2 = a * a;

            float3 sum    = float3(0, 0, 0);
            float  weight = 0.0f;
            for (uint i = 0; i < sampleCount; i++)
            {
                float2 xi = Hammersley(i, sampleCount);
                float3 H  = ImportanceSampleGGX(xi, N, a2);
                float3 L  = normalize(2.0f * dot(V, H) * H - V);

                float NdotL = saturate(dot(N, L));
                if (NdotL > 0.0f)
                {
                    sum    += srcTex.SampleLevel(srcSmp, L, 0).rgb * NdotL;
                    weight += NdotL;
                }
            }

            sum /= max(weight, 1e-5f);
            return float4(sum, 1.0f);
        }
        """;

    private const string IrradianceFragmentHlsl = """
        cbuffer Right   : register(b0, space3) { float4 Right;   }; // xyz=right,   w=unused
        cbuffer UpN     : register(b1, space3) { float4 UpN;     }; // xyz=up,      w=sampleCount
        cbuffer Forward : register(b2, space3) { float4 Forward; }; // xyz=forward, w=unused

        TextureCube<float4> srcTex : register(t0, space2);
        SamplerState        srcSmp : register(s0, space2);

        struct Input { float2 Ndc : TEXCOORD0; };

        static const float PI = 3.14159265f;

        float RadicalInverseVdC(uint bits)
        {
            bits = (bits << 16u) | (bits >> 16u);
            bits = ((bits & 0x55555555u) << 1u) | ((bits & 0xAAAAAAAAu) >> 1u);
            bits = ((bits & 0x33333333u) << 2u) | ((bits & 0xCCCCCCCCu) >> 2u);
            bits = ((bits & 0x0F0F0F0Fu) << 4u) | ((bits & 0xF0F0F0F0u) >> 4u);
            bits = ((bits & 0x00FF00FFu) << 8u) | ((bits & 0xFF00FF00u) >> 8u);
            return float(bits) * 2.3283064365386963e-10f;
        }

        float2 Hammersley(uint i, uint n)
        {
            return float2(float(i) / float(n), RadicalInverseVdC(i));
        }

        float4 main(Input input) : SV_Target0
        {
            uint sampleCount = (uint)UpN.w;

            // Per-pixel outward direction = N.
            float3 N = normalize(Forward.xyz
                               + input.Ndc.x * Right.xyz
                               + input.Ndc.y * UpN.xyz);

            // Tangent frame around N for cosine-weighted hemisphere
            // sampling.
            float3 worldUp = abs(N.y) < 0.999f ? float3(0, 1, 0) : float3(1, 0, 0);
            float3 T = normalize(cross(worldUp, N));
            float3 B = cross(N, T);

            // Cosine-weighted sampling cancels the cos(theta) factor in
            // the irradiance integrand, so the estimator is just the
            // average of sampled environment colors.
            float3 sum = float3(0, 0, 0);
            for (uint i = 0; i < sampleCount; i++)
            {
                float2 xi = Hammersley(i, sampleCount);
                float  phi      = 2.0f * PI * xi.x;
                float  cosTheta = sqrt(1.0f - xi.y);
                float  sinTheta = sqrt(xi.y);
                float3 local    = float3(cos(phi) * sinTheta,
                                         sin(phi) * sinTheta,
                                         cosTheta);
                float3 dir = normalize(T * local.x + B * local.y + N * local.z);
                sum += srcTex.SampleLevel(srcSmp, dir, 0).rgb;
            }

            sum /= float(sampleCount);
            return float4(sum, 1.0f);
        }
        """;

    private const string SpecularLutFragmentHlsl = """
        cbuffer Params : register(b0, space3) { float4 Params; }; // x = sampleCount

        struct Input { float2 Ndc : TEXCOORD0; };

        static const float PI = 3.14159265f;

        float RadicalInverseVdC(uint bits)
        {
            bits = (bits << 16u) | (bits >> 16u);
            bits = ((bits & 0x55555555u) << 1u) | ((bits & 0xAAAAAAAAu) >> 1u);
            bits = ((bits & 0x33333333u) << 2u) | ((bits & 0xCCCCCCCCu) >> 2u);
            bits = ((bits & 0x0F0F0F0Fu) << 4u) | ((bits & 0xF0F0F0F0u) >> 4u);
            bits = ((bits & 0x00FF00FFu) << 8u) | ((bits & 0xFF00FF00u) >> 8u);
            return float(bits) * 2.3283064365386963e-10f;
        }

        float2 Hammersley(uint i, uint n)
        {
            return float2(float(i) / float(n), RadicalInverseVdC(i));
        }

        float4 main(Input input) : SV_Target0
        {
            uint sampleCount = (uint)Params.x;

            // Map fullscreen-tri NDC into (NdotV, roughness). Y is
            // flipped so the top row corresponds to roughness = 0,
            // matching how the LUT is sampled in PbrShaders.LitPbr
            // (UV with V increasing downward).
            float NdotV     = saturate((input.Ndc.x + 1.0f) * 0.5f);
            float roughness = saturate((1.0f - input.Ndc.y) * 0.5f);

            // Tangent-space view vector from NdotV (N = +Z).
            float vx = sqrt(max(0.0f, 1.0f - NdotV * NdotV));
            float vz = NdotV;

            float a = roughness * roughness;
            float k = a * 0.5f; // Smith-GGX k for IBL.

            float scale = 0.0f;
            float bias  = 0.0f;
            for (uint i = 0; i < sampleCount; i++)
            {
                float2 xi = Hammersley(i, sampleCount);

                // Importance-sample GGX half-vector in tangent space.
                float phi      = 2.0f * PI * xi.x;
                float cosTheta = sqrt((1.0f - xi.y) / (1.0f + (a * a - 1.0f) * xi.y));
                float sinTheta = sqrt(max(0.0f, 1.0f - cosTheta * cosTheta));

                float hx = sinTheta * cos(phi);
                float hy = sinTheta * sin(phi);
                float hz = cosTheta;

                // L = reflect(-V, H); V.y = 0 so vDotH skips the y term.
                float vDotH = vx * hx + vz * hz;
                float lz    = 2.0f * vDotH * hz - vz;

                float NdotL = max(lz, 0.0f);
                if (NdotL > 0.0f)
                {
                    float NdotH  = max(hz, 0.0f);
                    float vDotHc = max(vDotH, 0.0f);

                    float gV = NdotV / (NdotV * (1.0f - k) + k);
                    float gL = NdotL / (NdotL * (1.0f - k) + k);
                    float gVis = (gV * gL * vDotHc) / (NdotH * NdotV);

                    float fc = pow(1.0f - vDotHc, 5.0f);
                    scale += (1.0f - fc) * gVis;
                    bias  += fc * gVis;
                }
            }

            scale /= float(sampleCount);
            bias  /= float(sampleCount);
            return float4(scale, bias, 0.0f, 1.0f);
        }
        """;
}
