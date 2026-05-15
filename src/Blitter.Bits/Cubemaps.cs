using System.Numerics;
using System.Runtime.InteropServices;

namespace Blitter.Bits;

/// <summary>
/// Pre-built procedural cubemaps.
/// </summary>
public static class Cubemaps
{
    private static Cubemap? s_sky;
    private static CubeTexture? s_skyDiffuse;
    private static CubeTexture? s_skySpecular;
    private static Cubemap? s_skySunless;
    private static CubeTexture? s_skySunlessDiffuse;
    private static CubeTexture? s_skySunlessSpecular;
    private static Cubemap? s_skyFlat;
    private static CubeTexture? s_skyFlatDiffuse;
    private static CubeTexture? s_skyFlatSpecular;
    private static Cubemap? s_black;

    /// <summary>
    /// Default procedural sky: 
    /// a blue zenith fading to pale horizon, warm ground below, with a sun disc in the upper-right. 
    /// Useful as a drop-in environment for demos when no specific environment map is supplied.
    /// </summary>
    public static Cubemap Sky => s_sky ??= CreateSky();

    /// <summary>
    /// Diffuse environment map derived from <see cref="Sky"/>: a heavily
    /// blurred version of the sky that captures the soft, ambient tint
    /// a matte surface picks up from its surroundings. Plug into
    /// <see cref="SkyLight.Diffuse"/>.
    /// </summary>
    public static CubeTexture SkyDiffuse => s_skyDiffuse ??= CreateDiffuse(Sky);

    /// <summary>
    /// Specular environment map derived from <see cref="Sky"/>: a
    /// mipmapped cubemap where mip 0 is the sky as a mirror sees it
    /// and each higher mip is pre-blurred for a rougher surface. Plug
    /// into <see cref="SkyLight.Specular"/>.
    /// </summary>
    public static CubeTexture SkySpecular => s_skySpecular ??= CreateSpecular(Sky);

    /// <summary>
    /// Like <see cref="Sky"/> but with the sun disc omitted. Use as
    /// the source for IBL when you have a separate directional light
    /// driving direct sun lighting -- avoids "two suns" reflections
    /// (one from the directional light, one baked into the sky).
    /// </summary>
    public static Cubemap SkySunless => s_skySunless ??= CreateSky(sunAngularRadius: 0f);

    /// <summary>
    /// Diffuse environment map derived from <see cref="SkySunless"/>.
    /// </summary>
    public static CubeTexture SkySunlessDiffuse => s_skySunlessDiffuse ??= CreateDiffuse(SkySunless);

    /// <summary>
    /// Specular environment map derived from <see cref="SkySunless"/>.
    /// </summary>
    public static CubeTexture SkySunlessSpecular => s_skySunlessSpecular ??= CreateSpecular(SkySunless);

    /// <summary>
    /// Uniform-tint sky: same color in every direction, no sun, no
    /// horizon band, no ground. Use as a neutral ambient IBL source
    /// for samples and material previews when any directional bright
    /// feature would distract -- shiny spheres reflect a flat tone
    /// instead of any distinguishable spot.
    /// </summary>
    public static Cubemap SkyFlat => s_skyFlat ??= CreateSky(
        zenith: new Color(150, 170, 200),
        horizon: new Color(150, 170, 200),
        ground: new Color(150, 170, 200),
        sunAngularRadius: 0f);

    /// <summary>
    /// Diffuse environment map derived from <see cref="SkyFlat"/>.
    /// </summary>
    public static CubeTexture SkyFlatDiffuse => s_skyFlatDiffuse ??= CreateDiffuse(SkyFlat);

    /// <summary>
    /// Specular environment map derived from <see cref="SkyFlat"/>.
    /// </summary>
    public static CubeTexture SkyFlatSpecular => s_skyFlatSpecular ??= CreateSpecular(SkyFlat);

    /// <summary>
    /// 1x1 black cubemap. Use as a zero-energy IBL source: feeding it
    /// to the diffuse + specular slots of a <see cref="SkyLight"/>
    /// makes the environment term multiply out to zero, so PBR
    /// materials fall back to pure direct lighting (ambient +
    /// directional + point) with no sky
    /// contribution.
    /// </summary>
    public static Cubemap Black => s_black ??= Cubemap.Create(
        Textures.Black, Textures.Black,
        Textures.Black, Textures.Black,
        Textures.Black, Textures.Black);

    /// <summary>
    /// Creates a cubemap by evaluating <paramref name="shade"/> per
    /// pixel with the outward direction through that pixel. Each
    /// face is <paramref name="faceSize"/>×<paramref name="faceSize"/>
    /// in <see cref="PixelFormat.ABGR8888"/>; HDR output is clipped
    /// to 0..255 per channel.
    /// </summary>
    /// <param name="faceSize">Pixel size of each cube face.</param>
    /// <param name="shade">Direction → color function. Direction is a unit vector pointing outward from the cube's centre.</param>
    public static Cubemap Create(int faceSize, Func<Vector3, Color> shade)
    {
        ArgumentNullException.ThrowIfNull(shade);
        return Create(faceSize, (_, dir) => shade(dir));
    }

    /// <summary>
    /// Face-aware <see cref="Create(int, Func{Vector3, Color})"/>: the
    /// shader also receives which face the pixel belongs to. Use this
    /// when a procedural pattern needs to vary per face (debug grids,
    /// per-face noise seeds) rather than purely by direction.
    /// </summary>
    /// <param name="faceSize">Pixel size of each cube face.</param>
    /// <param name="shade">(Face, direction) → color function. Direction is a unit vector pointing outward from the cube's centre.</param>
    public static Cubemap Create(int faceSize, Func<CubeFace, Vector3, Color> shade)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(faceSize, 1);
        ArgumentNullException.ThrowIfNull(shade);

        var posX = CreateFace(faceSize, CubeFace.PositiveX, shade);
        var negX = CreateFace(faceSize, CubeFace.NegativeX, shade);
        var posY = CreateFace(faceSize, CubeFace.PositiveY, shade);
        var negY = CreateFace(faceSize, CubeFace.NegativeY, shade);
        var posZ = CreateFace(faceSize, CubeFace.PositiveZ, shade);
        var negZ = CreateFace(faceSize, CubeFace.NegativeZ, shade);
        return Cubemap.Create(posX, negX, posY, negY, posZ, negZ);
    }

    /// <summary>
    /// Creates a procedural sky cubemap. The sky is a zenith→horizon→
    /// ground three-stop gradient driven by <c>dir.Y</c>, with an
    /// optional sun disc.
    /// </summary>
    /// <param name="faceSize">Pixel size of each cube face. 256 is plenty for a skybox; smaller for diffuse-IBL source.</param>
    /// <param name="zenith">Color at <c>dir = (0, 1, 0)</c>.</param>
    /// <param name="horizon">Color at <c>dir.Y = 0</c>.</param>
    /// <param name="ground">Color at <c>dir = (0, -1, 0)</c>.</param>
    /// <param name="sunDirection">Unit vector pointing toward the sun. Defaults to a high-noon-ish direction in the upper-right.</param>
    /// <param name="sunColor">Sun disc color. Defaults to bright warm white.</param>
    /// <param name="sunAngularRadius">Sun disc half-angle in radians. ~0.0046 is the real sun; 0.04 reads as obviously sun-shaped at default face sizes. Pass 0 (or any non-positive value) to omit the sun entirely.</param>
    public static Cubemap CreateSky(
        int faceSize = 256,
        Color? zenith = null,
        Color? horizon = null,
        Color? ground = null,
        Vector3? sunDirection = null,
        Color? sunColor = null,
        float sunAngularRadius = 0.04f)
    {
        var z = zenith ?? new Color(60, 110, 200);
        var h = horizon ?? new Color(200, 215, 235);
        var g = ground ?? new Color(70, 60, 50);
        var sc = sunColor ?? new Color(255, 245, 220);
        // Default sun: above horizon, off to the right and slightly
        // forward. Normalised so the dot product test against pixel
        // directions works without rescaling.
        var sd = Vector3.Normalize(sunDirection ?? new Vector3(0.5f, 0.5f, 0.3f));
        bool hasSun = sunAngularRadius > 0f;
        // Convert angular radius to a dot-product threshold once;
        // cheaper per-pixel than acos().
        float cosSunOuter = MathF.Cos(sunAngularRadius);
        // Soft edge: blend over the inner 10% of the disc. Without
        // this the sun reads as a hard-edged bitmap circle.
        float cosSunInner = MathF.Cos(sunAngularRadius * 0.9f);

        return Create(faceSize, dir =>
        {
            float y = Math.Clamp(dir.Y, -1f, 1f);

            // Three-stop gradient: ground -> horizon as y goes -1->0,
            // horizon -> zenith as y goes 0->1. Smoothstep at the
            // horizon avoids a hard seam between sky and ground.
            Color sky;
            if (y >= 0f)
            {
                float t = Smoothstep(0f, 1f, y);
                sky = Color.Lerp(h, z, t);
            }
            else
            {
                float t = Smoothstep(0f, 1f, -y);
                sky = Color.Lerp(h, g, t);
            }

            // Sun disc: only contributes above the horizon and only
            // within the angular cone around the sun direction.
            if (hasSun && y > 0f)
            {
                float cosAngle = Vector3.Dot(dir, sd);
                if (cosAngle > cosSunOuter)
                {
                    // 0 at outer edge, 1 inside the inner radius.
                    float k = Smoothstep(cosSunOuter, cosSunInner, cosAngle);
                    sky = Color.Lerp(sky, sc, k);
                }
            }

            return sky;
        });
    }

    /// <summary>
    /// Creates a diffuse environment cubemap from <paramref name="source"/>
    /// on the GPU: for every output direction N, integrates
    /// <c>source(ω) · cos(θ) dω</c> over the hemisphere around N.
    /// Output is heavily blurred -- a small <paramref name="faceSize"/>
    /// (32 is the conventional default) is plenty.
    /// </summary>
    /// <param name="source">Source environment cube.</param>
    /// <param name="faceSize">Edge length per face of the destination. 32 is plenty -- the integrand is very smooth.</param>
    /// <param name="samples">Cosine-weighted hemisphere samples per pixel.</param>
    public static GpuCubemap CreateDiffuse(
        CubeTexture source,
        int faceSize = 32,
        int samples = 256)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(faceSize);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(samples);

        var dest = GpuCubemap.Create(faceSize, levels: 1, renderTarget: true);
        var device = GpuDevice.Default;
        var mesh = IblShaders.FullscreenTri;
        var shader = IblShaders.DiffuseShader;

        foreach (var face in CubeFaceExtensions.All)
        {
            var forward = face.GetForward();
            var up = face.GetUp();
            // Right = up x forward gives the D3D/Vulkan cube-face image
            // orientation: top of the face image is +up.
            var right = Vector3.Normalize(Vector3.Cross(up, forward));

            var args = new IblShaders.DiffuseArgs
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
    /// Creates a specular environment cubemap from
    /// <paramref name="source"/> on the GPU for image-based lighting.
    /// Produces a mipmapped cubemap where mip <c>i</c> is the
    /// GGX-distribution importance-sampled integral of the environment
    /// at roughness <c>i / (levels - 1)</c>. Mip 0 (roughness 0) is the
    /// unfiltered environment downsampled to <paramref name="faceSize"/>;
    /// the highest mip (roughness 1) is fully blurred. Shaders read
    /// this by the reflection vector at LOD
    /// <c>roughness * (levels - 1)</c>.
    /// </summary>
    /// <param name="source">Source environment cube (HDR sky, etc.).</param>
    /// <param name="faceSize">Edge length per face of the destination.</param>
    /// <param name="levels">Mip-level count; <c>null</c> picks the full chain.</param>
    /// <param name="samples">GGX importance samples per pixel per face.</param>
    public static GpuCubemap CreateSpecular(
        CubeTexture source,
        int faceSize = 128,
        int? levels = null,
        int samples = 256)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(faceSize);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(samples);

        // floor(log2(size)) + 1 -- full chain down to 1x1.
        int levelCount = levels ?? Math.Max(1, (int)Math.Floor(Math.Log2(faceSize)) + 1);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(levelCount);

        var dest = GpuCubemap.Create(faceSize, levels: levelCount, renderTarget: true);
        var device = GpuDevice.Default;
        var mesh = IblShaders.FullscreenTri;
        var shader = IblShaders.SpecularShader;

        foreach (var face in CubeFaceExtensions.All)
        {
            var forward = face.GetForward();
            var up = face.GetUp();
            var right = Vector3.Normalize(Vector3.Cross(up, forward));

            for (int mip = 0; mip < levelCount; mip++)
            {
                float roughness = levelCount == 1
                    ? 0f
                    : mip / (float)(levelCount - 1);
                var args = new IblShaders.SpecularArgs
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

    private static Bitmap CreateFace(int size, CubeFace face, Func<CubeFace, Vector3, Color> shade)
    {
        var image = Bitmap.Create(size, size, PixelFormat.ABGR8888);
        float inv = 2f / size;
        // Rows are independent; SetPixel writes distinct byte ranges
        // per (x, y), so parallelising the outer loop is safe.
        System.Threading.Tasks.Parallel.For(0, size, y =>
        {
            // Image v in [-1, 1]; matches D3D / Vulkan convention
            // where image-row 0 is the "top" of the face as seen
            // looking outward through it.
            float v = (y + 0.5f) * inv - 1f;
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) * inv - 1f;
                var dir = Vector3.Normalize(FaceUVToDirection(face, u, v));
                image.SetPixel(x, y, shade(face, dir));
            }
        });
        return image;
    }

    // Standard D3D / Vulkan cubemap unprojection. (u, v) are the face's
    // image coordinates remapped to [-1, 1] with v growing downward;
    // result is a (not-yet-normalised) outward direction in world space.
    private static Vector3 FaceUVToDirection(CubeFace face, float u, float v) => face switch
    {
        CubeFace.PositiveX => new Vector3( 1f,  -v,  -u),
        CubeFace.NegativeX => new Vector3(-1f,  -v,   u),
        CubeFace.PositiveY => new Vector3( u,   1f,   v),
        CubeFace.NegativeY => new Vector3( u,  -1f,  -v),
        CubeFace.PositiveZ => new Vector3( u,  -v,   1f),
        CubeFace.NegativeZ => new Vector3(-u,  -v,  -1f),
        _ => throw new ArgumentOutOfRangeException(nameof(face), face, null),
    };

    private static float Smoothstep(float edge0, float edge1, float x)
    {
        float t = Math.Clamp((x - edge0) / (edge1 - edge0), 0f, 1f);
        return t * t * (3f - 2f * t);
    }
}

// GPU shader infrastructure for the IBL cubemap integrators above.
// Kept file-scoped: only Cubemaps.CreateDiffuse and
// Cubemaps.CreateSpecular consume these resources.
file static class IblShaders
{
    public static Mesh<Vertex3D> FullscreenTri => s_fullscreenTri.Value;
    public static Shader<Vertex3D, SpecularArgs> SpecularShader => s_specularShader.Value;
    public static Shader<Vertex3D, DiffuseArgs> DiffuseShader => s_diffuseShader.Value;

    // Three Vector4 cbuffer slots, 48 bytes total. Packs scalar params
    // into the .w of vectors that have a spare component.
    [StructLayout(LayoutKind.Sequential)]
    public struct SpecularArgs
    {
        public Vector4 RightAndRoughness;   // xyz = face right,   w = roughness
        public Vector4 UpAndSamples;        // xyz = face up,      w = sample count
        public Vector4 Forward;             // xyz = face forward, w = unused
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DiffuseArgs
    {
        public Vector4 Right;               // xyz = face right,   w = unused
        public Vector4 UpAndSamples;        // xyz = face up,      w = sample count
        public Vector4 Forward;             // xyz = face forward, w = unused
    }

    // Single fullscreen triangle in clip space; vertex shader passes
    // positions through and the fragment uses the interpolated xy as
    // NDC to reconstruct the per-pixel view direction.
    private static readonly Lazy<Mesh<Vertex3D>> s_fullscreenTri = new(() =>
        Mesh.Create<Vertex3D>(new ReadOnlySpan<Vertex3D>(new[]
        {
            new Vertex3D(-1f, -1f, 0f),
            new Vertex3D( 3f, -1f, 0f),
            new Vertex3D(-1f,  3f, 0f),
        })));

    private static readonly Lazy<Shader<Vertex3D, SpecularArgs>> s_specularShader = new(() =>
        new Shader<Vertex3D, SpecularArgs>(
            new VertexShader(VertexHlsl),
            new FragmentShader(SpecularFragmentHlsl),
            Vertex3D.ShaderVertexLayout,
            new ShaderArgsLayout(
                new ShaderArgElement(ShaderArgStage.Fragment, 0, ShaderArgKind.Float4),
                new ShaderArgElement(ShaderArgStage.Fragment, 1, ShaderArgKind.Float4),
                new ShaderArgElement(ShaderArgStage.Fragment, 2, ShaderArgKind.Float4)),
            ShaderTextureLayout.SingleTextureCube));

    private static readonly Lazy<Shader<Vertex3D, DiffuseArgs>> s_diffuseShader = new(() =>
        new Shader<Vertex3D, DiffuseArgs>(
            new VertexShader(VertexHlsl),
            new FragmentShader(DiffuseFragmentHlsl),
            Vertex3D.ShaderVertexLayout,
            new ShaderArgsLayout(
                new ShaderArgElement(ShaderArgStage.Fragment, 0, ShaderArgKind.Float4),
                new ShaderArgElement(ShaderArgStage.Fragment, 1, ShaderArgKind.Float4),
                new ShaderArgElement(ShaderArgStage.Fragment, 2, ShaderArgKind.Float4)),
            ShaderTextureLayout.SingleTextureCube));

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

    private const string SpecularFragmentHlsl = """
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

    private const string DiffuseFragmentHlsl = """
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
}
