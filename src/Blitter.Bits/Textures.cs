using System.Numerics;
using System.Runtime.InteropServices;

namespace Blitter.Bits;

/// <summary>
/// Catalog of process-shared images that PBR and other shaders pull
/// from. Each property lazy-initializes on first access and lives for
/// the rest of the process; callers must not <see cref="Texture2D.Dispose"/>
/// the returned images.
/// </summary>
public static class Textures
{
    private static Texture2D? s_white;
    private static Texture2D? s_black;
    private static Texture2D? s_specularLut;

    /// <summary>
    /// 1×1 opaque white image. Use as a placeholder when a shader
    /// expects a texture but the material has none -- the shader's
    /// per-channel factor then passes through unchanged.
    /// </summary>
    public static Texture2D White => s_white ??= CreateSolid(Color.White);

    /// <summary>
    /// 1×1 opaque black image. Use as a placeholder for additive
    /// texture slots (emissive, etc.) so the shader's contribution
    /// reduces to zero when no texture is supplied.
    /// </summary>
    public static Texture2D Black => s_black ??= CreateSolid(Color.Black);

    /// <summary>
    /// 256×256 precomputed split-sum BRDF integration texture used by
    /// PBR specular image-based lighting. R = scale, G = bias for the
    /// Fresnel/visibility term; sample with U = NdotV, V = roughness.
    /// </summary>
    public static Texture2D SpecularLut => s_specularLut ??= CreateSpecularLut();

    /// <summary>
    /// Generates a specular LUT (lookup table) on the GPU and downloads
    /// it to a CPU <see cref="Bitmap"/>. Returned as a CPU image so it
    /// can be consumed by both 2D and 3D renderers; the GPU still does
    /// all the integration work.
    /// </summary>
    /// <param name="size">Edge length of the square LUT.</param>
    /// <param name="samples">Importance samples per texel.</param>
    public static Bitmap CreateSpecularLut(int size = 256, int samples = 512)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(samples);

        var dest = Bitmap.Create(size, size, PixelFormat.ABGR8888);
        var mesh = SpecularLutShader.FullscreenTri;
        var shader = SpecularLutShader.Shader;
        var args = new SpecularLutShader.SpecularLutArgs { Params = new Vector4(samples, 0f, 0f, 0f) };

        dest.Render3D(rd => rd.DrawMeshRaw(mesh, shader, in args));
        return dest;
    }

    private static Texture2D CreateSolid(Color color)
    {
        var image = Bitmap.Create(1, 1, PixelFormat.ABGR8888);
        image.SetPixel(0, 0, color);
        return image;
    }
}

// GPU shader infrastructure for the specular LUT integrator above.
// Kept file-scoped: only Textures.CreateSpecularLut consumes it.
file static class SpecularLutShader
{
    public static Mesh<Vertex3D> FullscreenTri => s_fullscreenTri.Value;
    public static Shader<Vertex3D, SpecularLutArgs> Shader => s_shader.Value;

    [StructLayout(LayoutKind.Sequential)]
    public struct SpecularLutArgs
    {
        public Vector4 Params;              // x = sample count
    }

    private static readonly Lazy<Mesh<Vertex3D>> s_fullscreenTri = new(() =>
        Mesh.Create<Vertex3D>(new ReadOnlySpan<Vertex3D>(new[]
        {
            new Vertex3D(-1f, -1f, 0f),
            new Vertex3D( 3f, -1f, 0f),
            new Vertex3D(-1f,  3f, 0f),
        })));

    private static readonly Lazy<Shader<Vertex3D, SpecularLutArgs>> s_shader = new(() =>
        new Shader<Vertex3D, SpecularLutArgs>(
            new VertexShader(VertexHlsl),
            new FragmentShader(FragmentHlsl),
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

    private const string FragmentHlsl = """
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
