using System.Numerics;

namespace Blitter.Bits;

/// <summary>
/// Pre-baked procedural cubemaps.
/// </summary>
public static class Cubemaps
{
    private static Cubemap? s_sky;
    private static CubeTexture? s_skyIrradiance;
    private static CubeTexture? s_skyPrefiltered;

    /// <summary>
    /// Default procedural sky: 
    /// a blue zenith fading to pale horizon, warm ground below, with a sun disc in the upper-right. 
    /// Useful as a drop-in environment for demos when no specific environment map is supplied.
    /// </summary>
    public static Cubemap Sky => s_sky ??= BakeSky();

    /// <summary>
    /// Diffuse irradiance map derived from <see cref="Sky"/>: 
    /// the cosine-weighted hemisphere integral of the sky cubemap at every surface-normal direction. 
    /// Sample by surface normal to get the diffuse environment term used in image-based lighting.
    /// </summary>
    public static CubeTexture SkyIrradiance => s_skyIrradiance ??= EnvironmentBaker.BakeIrradiance(Sky);

    /// <summary>
    /// Prefiltered specular environment map derived from <see cref="Sky"/>.
    /// A mipmapped cubemap where mip <c>i</c> is the GGX-integrated
    /// reflection of the sky at roughness <c>i / (levels - 1)</c>.
    /// Sample by the reflection vector at <c>roughness * (levels - 1)</c>
    /// to get the specular environment term used in image-based lighting.
    /// </summary>
    public static CubeTexture SkyPrefiltered => s_skyPrefiltered ??= EnvironmentBaker.BakePrefilteredSpecular(Sky);

    /// <summary>
    /// Bakes a cubemap by evaluating <paramref name="shade"/> per
    /// pixel with the outward direction through that pixel. Each
    /// face is <paramref name="faceSize"/>×<paramref name="faceSize"/>
    /// in <see cref="PixelFormat.ABGR8888"/>; HDR output is clipped
    /// to 0..255 per channel.
    /// </summary>
    /// <param name="faceSize">Pixel size of each cube face.</param>
    /// <param name="shade">Direction → color function. Direction is a unit vector pointing outward from the cube's centre.</param>
    public static Cubemap Bake(int faceSize, Func<Vector3, Color> shade)
    {
        ArgumentNullException.ThrowIfNull(shade);
        return Bake(faceSize, (_, dir) => shade(dir));
    }

    /// <summary>
    /// Face-aware <see cref="Bake(int, Func{Vector3, Color})"/>: the
    /// shader also receives which face the pixel belongs to. Use this
    /// when a procedural pattern needs to vary per face (debug grids,
    /// per-face noise seeds) rather than purely by direction.
    /// </summary>
    /// <param name="faceSize">Pixel size of each cube face.</param>
    /// <param name="shade">(Face, direction) → color function. Direction is a unit vector pointing outward from the cube's centre.</param>
    public static Cubemap Bake(int faceSize, Func<CubeFace, Vector3, Color> shade)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(faceSize, 1);
        ArgumentNullException.ThrowIfNull(shade);

        var posX = BakeFace(faceSize, CubeFace.PositiveX, shade);
        var negX = BakeFace(faceSize, CubeFace.NegativeX, shade);
        var posY = BakeFace(faceSize, CubeFace.PositiveY, shade);
        var negY = BakeFace(faceSize, CubeFace.NegativeY, shade);
        var posZ = BakeFace(faceSize, CubeFace.PositiveZ, shade);
        var negZ = BakeFace(faceSize, CubeFace.NegativeZ, shade);
        return Cubemap.Create(posX, negX, posY, negY, posZ, negZ);
    }

    /// <summary>
    /// Bakes a procedural sky cubemap. The sky is a zenith→horizon→
    /// ground three-stop gradient driven by <c>dir.Y</c>, with an
    /// optional sun disc.
    /// </summary>
    /// <param name="faceSize">Pixel size of each cube face. 256 is plenty for a skybox; smaller for irradiance source.</param>
    /// <param name="zenith">Color at <c>dir = (0, 1, 0)</c>.</param>
    /// <param name="horizon">Color at <c>dir.Y = 0</c>.</param>
    /// <param name="ground">Color at <c>dir = (0, -1, 0)</c>.</param>
    /// <param name="sunDirection">Unit vector pointing toward the sun. Defaults to a high-noon-ish direction in the upper-right.</param>
    /// <param name="sunColor">Sun disc color. Defaults to bright warm white.</param>
    /// <param name="sunAngularRadius">Sun disc half-angle in radians. ~0.0046 is the real sun; 0.04 reads as obviously sun-shaped at default face sizes. Pass 0 (or any non-positive value) to omit the sun entirely.</param>
    public static Cubemap BakeSky(
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

        return Bake(faceSize, dir =>
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
    /// Bakes a diffuse irradiance cubemap from <paramref name="source"/>:
    /// for every output direction N, integrates
    /// <c>source(ω) · cos(θ) dω</c> over the hemisphere around N.
    /// Output is heavily blurred -- a small <paramref name="faceSize"/>
    /// (32 is the conventional default) is plenty.
    /// </summary>
    /// <param name="source">Environment cubemap to integrate.</param>
    /// <param name="faceSize">Pixel size of each output face. Defaults to 32; the integrand is low-frequency so larger doesn't help quality.</param>
    /// <param name="samples">Monte-Carlo samples per output pixel. 256 gives a clean result on the default LDR sky; raise if banding shows.</param>
    public static Cubemap BakeIrradiance(Cubemap source, int faceSize = 32, int samples = 256)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentOutOfRangeException.ThrowIfLessThan(faceSize, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(samples, 1);

        return Bake(faceSize, n => IntegrateIrradiance(source, n, samples));
    }

    /// <summary>
    /// Bakes a prefiltered specular environment cubemap from
    /// <paramref name="source"/> for image-based lighting. Produces a
    /// mipmapped cubemap where mip <c>i</c> is the GGX-distribution
    /// importance-sampled integral of the environment at roughness
    /// <c>i / (levels - 1)</c>. Mip 0 (roughness 0) is the unfiltered
    /// environment downsampled to <paramref name="faceSize"/>; the
    /// highest mip (roughness 1) is fully blurred. Shaders sample
    /// this by the reflection vector at LOD
    /// <c>roughness * (levels - 1)</c>.
    /// </summary>
    /// <param name="source">Environment cubemap to integrate.</param>
    /// <param name="faceSize">Pixel size of the base (mip 0) face. 128 is a good default; the chain auto-shrinks each level.</param>
    /// <param name="levels">Number of mip levels to bake. Defaults to a chain that ends at an 8×8 base level.</param>
    /// <param name="samples">Importance samples per pixel for rough mips. Higher reduces specular fireflies. 1024 is the common default.</param>
    public static Cubemap BakePrefilteredSpecular(
        Cubemap source,
        int faceSize = 128,
        int? levels = null,
        int samples = 1024)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentOutOfRangeException.ThrowIfLessThan(faceSize, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(samples, 1);

        // Default chain ends near 8×8. Each level halves; the last mip
        // is fully blurred so going smaller adds nothing visible.
        int defaultLevels = Math.Max(1, BitOperations.Log2((uint)faceSize) - 2);
        int levelCount = levels ?? defaultLevels;
        ArgumentOutOfRangeException.ThrowIfLessThan(levelCount, 1);

        // [face][mip] Bitmap grid; assembled into 6 MipmappedImages
        // and one Cubemap at the end.
        var chains = new Bitmap[6][];
        for (int f = 0; f < 6; f++)
            chains[f] = new Bitmap[levelCount];

        for (int mip = 0; mip < levelCount; mip++)
        {
            int mipSize = Math.Max(1, faceSize >> mip);
            // Roughness 0 at mip 0, 1 at the last mip. Single-level
            // chains degenerate to roughness 0 (an unfiltered copy).
            float roughness = levelCount == 1 ? 0f : (float)mip / (levelCount - 1);

            for (int f = 0; f < 6; f++)
            {
                var face = (CubeFace)f;
                chains[f][mip] = BakeFace(mipSize, face,
                    (_, n) => PrefilteredSpecular(source, n, roughness, samples));
            }
        }

        var posXLevels = MipmappedImage.Create(chains[0]);
        var negXLevels = MipmappedImage.Create(chains[1]);
        var posYLevels = MipmappedImage.Create(chains[2]);
        var negYLevels = MipmappedImage.Create(chains[3]);
        var posZLevels = MipmappedImage.Create(chains[4]);
        var negZLevels = MipmappedImage.Create(chains[5]);
        return Cubemap.Create(posXLevels, negXLevels, posYLevels, negYLevels, posZLevels, negZLevels);
    }

    // Split-sum specular pre-integration assuming V == R == N: estimates
    //   ∫ source(L) · GGX(H; roughness) · max(0, N·L) dω
    // / ∫                                  max(0, N·L) dω
    // with importance-sampled microfacet normals H. At roughness 0 the
    // GGX lobe collapses to a delta function, so sample the environment
    // directly to avoid degenerate accumulation.
    private static Color PrefilteredSpecular(Cubemap source, Vector3 n, float roughness, int samples)
    {
        if (roughness <= 0f)
            return SampleCubemap(source, n);

        // Tangent frame around N; same stability trick as the
        // irradiance integrator.
        Vector3 up = MathF.Abs(n.Y) < 0.999f ? Vector3.UnitY : Vector3.UnitX;
        Vector3 t = Vector3.Normalize(Vector3.Cross(up, n));
        Vector3 b = Vector3.Cross(n, t);

        float a = roughness * roughness;
        float a2 = a * a;

        Vector3 sum = Vector3.Zero;
        float weight = 0f;
        for (int i = 0; i < samples; i++)
        {
            var (u1, u2) = Hammersley(i, samples);
            // GGX importance sample for the half-vector H.
            float cosTheta = MathF.Sqrt((1f - u2) / (1f + (a2 - 1f) * u2));
            float sinTheta = MathF.Sqrt(MathF.Max(0f, 1f - cosTheta * cosTheta));
            float phi = MathF.Tau * u1;
            Vector3 hLocal = new(sinTheta * MathF.Cos(phi), sinTheta * MathF.Sin(phi), cosTheta);
            Vector3 h = t * hLocal.X + b * hLocal.Y + n * hLocal.Z;
            // L = reflect(-V, H) with V = N.
            Vector3 l = Vector3.Normalize(2f * Vector3.Dot(n, h) * h - n);

            float nDotL = Vector3.Dot(n, l);
            if (nDotL > 0f)
            {
                var c = SampleCubemap(source, l);
                sum += new Vector3(c.R, c.G, c.B) * nDotL;
                weight += nDotL;
            }
        }
        if (weight <= 0f)
            return SampleCubemap(source, n);

        sum /= weight;
        return new Color(
            (byte)Math.Clamp(sum.X, 0f, 255f),
            (byte)Math.Clamp(sum.Y, 0f, 255f),
            (byte)Math.Clamp(sum.Z, 0f, 255f));
    }

    // Cosine-weighted Monte-Carlo estimate of the irradiance at
    // surface-normal direction N. Cosine sampling cancels the cos(θ)
    // weight in the integrand, so the estimator is just the average
    // of the sampled environment colors.
    private static Color IntegrateIrradiance(Cubemap source, Vector3 n, int samples)
    {
        // Tangent frame around N. Picking the world axis least
        // aligned with N keeps the cross product numerically stable.
        Vector3 up = MathF.Abs(n.Y) < 0.999f ? Vector3.UnitY : Vector3.UnitX;
        Vector3 t = Vector3.Normalize(Vector3.Cross(up, n));
        Vector3 b = Vector3.Cross(n, t);

        Vector3 sum = Vector3.Zero;
        for (int i = 0; i < samples; i++)
        {
            var (u1, u2) = Hammersley(i, samples);
            // Cosine-weighted hemisphere sample in tangent space with
            // Z aligned to N. cos(θ) = sqrt(1 - u2) gives the density
            // proportional to cos(θ) we need.
            float phi = MathF.Tau * u1;
            float cosTheta = MathF.Sqrt(1f - u2);
            float sinTheta = MathF.Sqrt(u2);
            Vector3 local = new(sinTheta * MathF.Cos(phi), sinTheta * MathF.Sin(phi), cosTheta);
            Vector3 dir = t * local.X + b * local.Y + n * local.Z;
            var c = SampleCubemap(source, dir);
            sum += new Vector3(c.R, c.G, c.B);
        }
        sum /= samples;
        return new Color(
            (byte)Math.Clamp(sum.X, 0f, 255f),
            (byte)Math.Clamp(sum.Y, 0f, 255f),
            (byte)Math.Clamp(sum.Z, 0f, 255f));
    }

    // Hammersley low-discrepancy sequence. The bit-reversed Van der
    // Corput second coordinate gives an even sample distribution far
    // better than random in low sample counts.
    private static (float, float) Hammersley(int i, int n)
    {
        uint bits = (uint)i;
        bits = (bits << 16) | (bits >> 16);
        bits = ((bits & 0x55555555u) << 1) | ((bits & 0xAAAAAAAAu) >> 1);
        bits = ((bits & 0x33333333u) << 2) | ((bits & 0xCCCCCCCCu) >> 2);
        bits = ((bits & 0x0F0F0F0Fu) << 4) | ((bits & 0xF0F0F0F0u) >> 4);
        bits = ((bits & 0x00FF00FFu) << 8) | ((bits & 0xFF00FF00u) >> 8);
        float vdc = bits * 2.3283064365386963e-10f;
        return ((float)i / n, vdc);
    }

    // Direction → face + (x, y) pixel coordinates on that face,
    // inverse of FaceUVToDirection. Nearest-pixel sampling -- bilinear
    // would help precision but the integrand averaging already smears
    // hundreds of samples per output pixel.
    private static Color SampleCubemap(Cubemap c, Vector3 dir)
    {
        float ax = MathF.Abs(dir.X), ay = MathF.Abs(dir.Y), az = MathF.Abs(dir.Z);
        CubeFace face;
        float u, v, maj;
        if (ax >= ay && ax >= az)
        {
            maj = ax;
            if (dir.X > 0f) { face = CubeFace.PositiveX; u = -dir.Z; v = -dir.Y; }
            else            { face = CubeFace.NegativeX; u =  dir.Z; v = -dir.Y; }
        }
        else if (ay >= az)
        {
            maj = ay;
            if (dir.Y > 0f) { face = CubeFace.PositiveY; u =  dir.X; v =  dir.Z; }
            else            { face = CubeFace.NegativeY; u =  dir.X; v = -dir.Z; }
        }
        else
        {
            maj = az;
            if (dir.Z > 0f) { face = CubeFace.PositiveZ; u =  dir.X; v = -dir.Y; }
            else            { face = CubeFace.NegativeZ; u = -dir.X; v = -dir.Y; }
        }
        // dir is unit-length, so at least one component is >= 1/√3 and
        // maj is always > 0; no zero-division guard needed.
        u /= maj;
        v /= maj;
        int size = c.Size;
        int x = Math.Clamp((int)((u + 1f) * 0.5f * size), 0, size - 1);
        int y = Math.Clamp((int)((v + 1f) * 0.5f * size), 0, size - 1);
        return AsBitmap(c.GetFace(face)).GetPixel(x, y);
    }

    // CPU sampling needs raw pixels. Unwrap a mip chain to its base
    // level; anything else (e.g. a future GPU-only image) can't be
    // sampled on the CPU.
    private static Bitmap AsBitmap(Image image) => image switch
    {
        Bitmap bitmap => bitmap,
        MipmappedImage mipmapped => AsBitmap(mipmapped.Base),
        _ => throw new NotSupportedException(
            $"Cubemap face image of type {image.GetType().Name} cannot be sampled on the CPU."),
    };

    private static Bitmap BakeFace(int size, CubeFace face, Func<CubeFace, Vector3, Color> shade)
    {
        var image = Image.Create(size, size, PixelFormat.ABGR8888);
        float inv = 2f / size;
        // Rows are independent; SetPixel writes distinct byte ranges
        // per (x, y), so parallelising the outer loop is safe. This is
        // the hot path for `BakePrefilteredSpecular` -- one cube level
        // at default 128² with 1024 samples is millions of integrand
        // evaluations.
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
