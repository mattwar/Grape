namespace Blitter.Bits;

/// <summary>
/// Catalog of process-shared images that PBR and other shaders pull
/// from. Each property lazy-initializes on first access and lives for
/// the rest of the process; callers must not <see cref="Image.Dispose"/>
/// the returned images.
/// </summary>
public static class Textures
{
    private static Image? s_white;
    private static Image? s_black;
    private static Image? s_specularLut;

    /// <summary>
    /// 1×1 opaque white image. Use as a placeholder when a shader
    /// expects a texture but the material has none -- the shader's
    /// per-channel factor then passes through unchanged.
    /// </summary>
    public static Image White => s_white ??= CreateSolid(Color.White);

    /// <summary>
    /// 1×1 opaque black image. Use as a placeholder for additive
    /// texture slots (emissive, etc.) so the shader's contribution
    /// reduces to zero when no texture is supplied.
    /// </summary>
    public static Image Black => s_black ??= CreateSolid(Color.Black);

    /// <summary>
    /// 256×256 precomputed split-sum BRDF integration texture used by
    /// PBR specular image-based lighting. R = scale, G = bias for the
    /// Fresnel/visibility term; sample with U = NdotV, V = roughness.
    /// </summary>
    public static Image SpecularLut => s_specularLut ??= BakeSpecularLut();

    private static Image CreateSolid(Color color)
    {
        var image = Image.Create(1, 1, PixelFormat.ABGR8888);
        image.SetPixel(0, 0, color);
        return image;
    }

    // Bakes the split-sum environment BRDF (Karis 2013) on the CPU.
    // The integral is purely a function of (NdotV, roughness) -- no
    // scene inputs -- so we can fill the image directly via SetPixel
    // without going through the GPU render path. CPU also sidesteps
    // ImageGpuRenderer's 8-bit color target hard-coding; the output
    // here is still 8-bit-per-channel, but the math runs at full
    // float precision and we avoid an extra round-trip. Move to a GPU
    // bake into RGBA16F once ImageGpuRenderer supports float targets.
    private static Image BakeSpecularLut()
    {
        const int Size = 256;
        const int Samples = 512;

        var image = Image.Create(Size, Size, PixelFormat.ABGR8888);

        for (int y = 0; y < Size; y++)
        {
            float roughness = (y + 0.5f) / Size;
            for (int x = 0; x < Size; x++)
            {
                float nDotV = (x + 0.5f) / Size;
                var (scale, bias) = IntegrateBrdf(nDotV, roughness, Samples);
                byte r = (byte)Math.Clamp((int)MathF.Round(scale * 255f), 0, 255);
                byte g = (byte)Math.Clamp((int)MathF.Round(bias * 255f), 0, 255);
                image.SetPixel(x, y, new Color(r, g, 0, 255));
            }
        }

        return image;
    }

    // Split-sum integrand: returns (scale, bias) such that
    //   specularIBL ≈ prefilteredEnv * (F0 * scale + bias).
    // V is rebuilt in tangent space with N = (0, 0, 1), so NdotV
    // collapses to V.z. Importance-sampled GGX with the Smith G_Vis
    // visibility factor; Fc is the Schlick complement.
    private static (float scale, float bias) IntegrateBrdf(float nDotV, float roughness, int samples)
    {
        // Tangent-space view vector reconstructed from NdotV.
        float vx = MathF.Sqrt(1f - nDotV * nDotV);
        float vz = nDotV;

        float a = roughness * roughness;
        float k = a * 0.5f; // Smith-GGX k for IBL.

        float scale = 0f;
        float bias = 0f;

        for (int i = 0; i < samples; i++)
        {
            var xi = Hammersley(i, samples);

            // Importance-sample GGX in tangent space (N = +Z).
            float phi = 2f * MathF.PI * xi.x;
            float cosTheta = MathF.Sqrt((1f - xi.y) / (1f + (a * a - 1f) * xi.y));
            float sinTheta = MathF.Sqrt(MathF.Max(0f, 1f - cosTheta * cosTheta));

            float hx = sinTheta * MathF.Cos(phi);
            float hy = sinTheta * MathF.Sin(phi);
            float hz = cosTheta;

            // L = reflect(-V, H) = 2 (V·H) H - V.
            float vDotH = vx * hx + vz * hz; // V.y is 0.
            float lx = 2f * vDotH * hx - vx;
            float ly = 2f * vDotH * hy;
            float lz = 2f * vDotH * hz - vz;

            float nDotL = MathF.Max(lz, 0f);
            if (nDotL <= 0f)
                continue;

            float nDotH = MathF.Max(hz, 0f);
            float vDotHClamped = MathF.Max(vDotH, 0f);

            // Smith G with k = α²/2; the (NdotV * NdotH) terms below
            // come from converting D-weighted importance sampling
            // into the visibility-weighted estimator.
            float gV = nDotV / (nDotV * (1f - k) + k);
            float gL = nDotL / (nDotL * (1f - k) + k);
            float g = gV * gL;
            float gVis = (g * vDotHClamped) / (nDotH * nDotV);

            float fc = MathF.Pow(1f - vDotHClamped, 5f);
            scale += (1f - fc) * gVis;
            bias += fc * gVis;
        }

        return (scale / samples, bias / samples);
    }

    // Quasi-random sample point in [0,1)². The bit-reversed second
    // coordinate (Van der Corput sequence) gives a low-discrepancy
    // distribution that converges faster than uniform random.
    private static (float x, float y) Hammersley(int i, int n)
    {
        uint bits = (uint)i;
        bits = (bits << 16) | (bits >> 16);
        bits = ((bits & 0x55555555u) << 1) | ((bits & 0xAAAAAAAAu) >> 1);
        bits = ((bits & 0x33333333u) << 2) | ((bits & 0xCCCCCCCCu) >> 2);
        bits = ((bits & 0x0F0F0F0Fu) << 4) | ((bits & 0xF0F0F0F0u) >> 4);
        bits = ((bits & 0x00FF00FFu) << 8) | ((bits & 0xFF00FF00u) >> 8);
        float vdc = bits * 2.3283064365386963e-10f; // / 2^32
        return ((float)i / n, vdc);
    }
}
