namespace Blitter.Bits;

/// <summary>
/// Standard easing curves. 
/// All functions take and return a normalized parameter <c>t</c> in [0, 1]; outside that range the curves extrapolate.
/// Use these as the easing input to <c>float.Lerp</c>, <c>Vector3.Lerp</c>, and friends.
/// </summary>
public static class Easing
{
    private const float Pi = MathF.PI;
    private const float HalfPi = MathF.PI * 0.5f;
    private const float TwoPi = MathF.PI * 2f;

    // ---------- Sine ----------

    public static float InSine(float t) => 1f - MathF.Cos(t * HalfPi);
    public static float OutSine(float t) => MathF.Sin(t * HalfPi);
    public static float InOutSine(float t) => -(MathF.Cos(Pi * t) - 1f) * 0.5f;

    // ---------- Quadratic ----------

    public static float InQuad(float t) => t * t;
    public static float OutQuad(float t) { var u = 1f - t; return 1f - u * u; }
    public static float InOutQuad(float t) =>
        t < 0.5f ? 2f * t * t : 1f - MathF.Pow(-2f * t + 2f, 2f) * 0.5f;

    // ---------- Cubic ----------

    public static float InCubic(float t) => t * t * t;
    public static float OutCubic(float t) { var u = 1f - t; return 1f - u * u * u; }
    public static float InOutCubic(float t) =>
        t < 0.5f ? 4f * t * t * t : 1f - MathF.Pow(-2f * t + 2f, 3f) * 0.5f;

    // ---------- Quartic ----------

    public static float InQuart(float t) => t * t * t * t;
    public static float OutQuart(float t) { var u = 1f - t; return 1f - u * u * u * u; }
    public static float InOutQuart(float t) =>
        t < 0.5f ? 8f * t * t * t * t : 1f - MathF.Pow(-2f * t + 2f, 4f) * 0.5f;

    // ---------- Quintic ----------

    public static float InQuint(float t) => t * t * t * t * t;
    public static float OutQuint(float t) { var u = 1f - t; return 1f - u * u * u * u * u; }
    public static float InOutQuint(float t) =>
        t < 0.5f ? 16f * t * t * t * t * t : 1f - MathF.Pow(-2f * t + 2f, 5f) * 0.5f;

    // ---------- Exponential ----------

    public static float InExpo(float t) =>
        t <= 0f ? 0f : MathF.Pow(2f, 10f * t - 10f);
    public static float OutExpo(float t) =>
        t >= 1f ? 1f : 1f - MathF.Pow(2f, -10f * t);
    public static float InOutExpo(float t)
    {
        if (t <= 0f) return 0f;
        if (t >= 1f) return 1f;
        return t < 0.5f
            ? MathF.Pow(2f, 20f * t - 10f) * 0.5f
            : (2f - MathF.Pow(2f, -20f * t + 10f)) * 0.5f;
    }

    // ---------- Circular ----------

    public static float InCirc(float t) => 1f - MathF.Sqrt(1f - t * t);
    public static float OutCirc(float t) { var u = t - 1f; return MathF.Sqrt(1f - u * u); }
    public static float InOutCirc(float t) =>
        t < 0.5f
            ? (1f - MathF.Sqrt(1f - 4f * t * t)) * 0.5f
            : (MathF.Sqrt(1f - MathF.Pow(-2f * t + 2f, 2f)) + 1f) * 0.5f;

    // ---------- Back (overshoots) ----------

    private const float BackC1 = 1.70158f;
    private const float BackC2 = BackC1 * 1.525f;
    private const float BackC3 = BackC1 + 1f;

    public static float InBack(float t) => BackC3 * t * t * t - BackC1 * t * t;
    public static float OutBack(float t)
    {
        var u = t - 1f;
        return 1f + BackC3 * u * u * u + BackC1 * u * u;
    }
    public static float InOutBack(float t) =>
        t < 0.5f
            ? MathF.Pow(2f * t, 2f) * ((BackC2 + 1f) * 2f * t - BackC2) * 0.5f
            : (MathF.Pow(2f * t - 2f, 2f) * ((BackC2 + 1f) * (t * 2f - 2f) + BackC2) + 2f) * 0.5f;

    // ---------- Elastic ----------

    private const float ElasticC4 = TwoPi / 3f;
    private const float ElasticC5 = TwoPi / 4.5f;

    public static float InElastic(float t)
    {
        if (t <= 0f) return 0f;
        if (t >= 1f) return 1f;
        return -MathF.Pow(2f, 10f * t - 10f) * MathF.Sin((t * 10f - 10.75f) * ElasticC4);
    }
    public static float OutElastic(float t)
    {
        if (t <= 0f) return 0f;
        if (t >= 1f) return 1f;
        return MathF.Pow(2f, -10f * t) * MathF.Sin((t * 10f - 0.75f) * ElasticC4) + 1f;
    }
    public static float InOutElastic(float t)
    {
        if (t <= 0f) return 0f;
        if (t >= 1f) return 1f;
        return t < 0.5f
            ? -(MathF.Pow(2f, 20f * t - 10f) * MathF.Sin((20f * t - 11.125f) * ElasticC5)) * 0.5f
            : MathF.Pow(2f, -20f * t + 10f) * MathF.Sin((20f * t - 11.125f) * ElasticC5) * 0.5f + 1f;
    }

    // ---------- Bounce ----------

    public static float OutBounce(float t)
    {
        const float n1 = 7.5625f;
        const float d1 = 2.75f;
        if (t < 1f / d1) return n1 * t * t;
        if (t < 2f / d1) { t -= 1.5f / d1; return n1 * t * t + 0.75f; }
        if (t < 2.5f / d1) { t -= 2.25f / d1; return n1 * t * t + 0.9375f; }
        t -= 2.625f / d1;
        return n1 * t * t + 0.984375f;
    }
    public static float InBounce(float t) => 1f - OutBounce(1f - t);
    public static float InOutBounce(float t) =>
        t < 0.5f
            ? (1f - OutBounce(1f - 2f * t)) * 0.5f
            : (1f + OutBounce(2f * t - 1f)) * 0.5f;
}
