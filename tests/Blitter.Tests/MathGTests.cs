using System.Numerics;
using Blitter.Bits;

namespace Blitter.Tests;

public class MathGTests
{
    private const float Eps = 1e-4f;

    // -------------------- Scalar --------------------

    [Theory]
    [InlineData(-1f, 0f)]
    [InlineData(0f, 0f)]
    [InlineData(0.5f, 0.5f)]
    [InlineData(1f, 1f)]
    [InlineData(2f, 1f)]
    public void Saturate_ClampsTo01(float input, float expected) =>
        Assert.Equal(expected, MathG.Saturate(input), Eps);

    [Theory]
    [InlineData(0f, 10f, 5f, 0.5f)]
    [InlineData(0f, 10f, 0f, 0f)]
    [InlineData(0f, 10f, 10f, 1f)]
    [InlineData(0f, 10f, -5f, -0.5f)] // extrapolates
    public void InverseLerp_MatchesLerpInverse(float a, float b, float v, float expected) =>
        Assert.Equal(expected, MathG.InverseLerp(a, b, v), Eps);

    [Fact]
    public void InverseLerp_DegenerateReturnsZero() =>
        Assert.Equal(0f, MathG.InverseLerp(5f, 5f, 5f));

    [Fact]
    public void Remap_PreservesRatio()
    {
        // Map [0,10] -> [100,200]; 5 should land at 150.
        Assert.Equal(150f, MathG.Remap(5f, 0f, 10f, 100f, 200f), Eps);
    }

    [Fact]
    public void SmoothStep_BoundsAndMidpoint()
    {
        Assert.Equal(0f, MathG.SmoothStep(0f, 1f, -1f), Eps);
        Assert.Equal(0f, MathG.SmoothStep(0f, 1f, 0f), Eps);
        Assert.Equal(0.5f, MathG.SmoothStep(0f, 1f, 0.5f), Eps);
        Assert.Equal(1f, MathG.SmoothStep(0f, 1f, 1f), Eps);
        Assert.Equal(1f, MathG.SmoothStep(0f, 1f, 2f), Eps);
    }

    [Fact]
    public void SmootherStep_BoundsAndMidpoint()
    {
        Assert.Equal(0f, MathG.SmootherStep(0f, 1f, 0f), Eps);
        Assert.Equal(0.5f, MathG.SmootherStep(0f, 1f, 0.5f), Eps);
        Assert.Equal(1f, MathG.SmootherStep(0f, 1f, 1f), Eps);
    }

    [Fact]
    public void Damp_HalvesGapEveryHalfLife()
    {
        // current=0, target=100, halfLife=1s -> after 1s gap is 50, after 2s gap is 25.
        var v = 0f;
        v = MathG.Damp(v, 100f, halfLife: 1f, dt: 1f);
        Assert.Equal(50f, v, Eps);
        v = MathG.Damp(v, 100f, halfLife: 1f, dt: 1f);
        Assert.Equal(75f, v, Eps);
    }

    [Fact]
    public void Damp_ZeroHalfLifeSnapsToTarget() =>
        Assert.Equal(100f, MathG.Damp(0f, 100f, halfLife: 0f, dt: 1f / 60f));

    [Fact]
    public void Damp_FramerateIndependent_TotalElapsed1Second()
    {
        // Integrating Damp over many small steps should converge to the
        // analytic answer (50% of the gap consumed after one half-life).
        var v = 0f;
        const float dt = 1f / 240f;
        for (int i = 0; i < 240; i++)
            v = MathG.Damp(v, 100f, halfLife: 1f, dt: dt);
        Assert.Equal(50f, v, 1e-2f);
    }

    [Fact]
    public void MoveToward_Scalar_StopsAtTarget()
    {
        Assert.Equal(5f, MathG.MoveToward(0f, 5f, 10f));  // overshoot clamped
        Assert.Equal(3f, MathG.MoveToward(0f, 5f, 3f));
        Assert.Equal(-3f, MathG.MoveToward(0f, -5f, 3f));
        Assert.Equal(5f, MathG.MoveToward(5f, 5f, 100f));
    }

    [Fact]
    public void MoveToward_Vector3_StopsAtTarget()
    {
        var r = MathG.MoveToward(Vector3.Zero, new Vector3(3, 4, 0), maxDelta: 10f);
        Assert.Equal(new Vector3(3, 4, 0), r);

        // length(3,4,0) = 5; step of 2.5 -> halfway
        var halfway = MathG.MoveToward(Vector3.Zero, new Vector3(3, 4, 0), 2.5f);
        Assert.Equal(1.5f, halfway.X, Eps);
        Assert.Equal(2f, halfway.Y, Eps);
    }

    // -------------------- Angles --------------------

    [Theory]
    [InlineData(0f, 0f)]
    [InlineData(179f, 179f)]
    [InlineData(180f, -180f)]
    [InlineData(181f, -179f)]
    [InlineData(360f, 0f)]
    [InlineData(540f, -180f)]
    [InlineData(-180f, -180f)]
    [InlineData(-181f, 179f)]
    public void WrapDegrees_MapsToHalfOpen180(float input, float expected) =>
        Assert.Equal(expected, MathG.WrapDegrees(input), Eps);

    [Theory]
    [InlineData(0f, 90f, 90f)]
    [InlineData(350f, 10f, 20f)]      // crossing 0
    [InlineData(10f, 350f, -20f)]
    [InlineData(0f, 180f, -180f)]     // exactly opposite -> -180
    public void ShortestArcDegrees(float from, float to, float expected) =>
        Assert.Equal(expected, MathG.ShortestArcDegrees(from, to), Eps);

    [Fact]
    public void WrapRadians_RoundTripsWithDegrees()
    {
        var rad = MathG.DegreesToRadians(540f);
        Assert.Equal(-MathF.PI, MathG.WrapRadians(rad), Eps);
    }

    // -------------------- Vector3 helpers --------------------

    [Fact]
    public void ProjectOnPlane_RemovesNormalComponent()
    {
        var v = new Vector3(1, 2, 3);
        var n = Vector3.UnitY; // plane = XZ
        var p = MathG.ProjectOnPlane(v, n);
        Assert.Equal(0f, p.Y, Eps);
        Assert.Equal(v.X, p.X, Eps);
        Assert.Equal(v.Z, p.Z, Eps);
    }

    [Fact]
    public void ProjectOnPlane_NonUnitNormalStillCorrect()
    {
        var v = new Vector3(1, 2, 3);
        var n = new Vector3(0, 5, 0); // same plane, scaled normal
        var p = MathG.ProjectOnPlane(v, n);
        Assert.Equal(0f, p.Y, Eps);
    }

    [Fact]
    public void ClampMagnitude_LimitsLength()
    {
        var v = new Vector3(3, 4, 0); // length 5
        var c = MathG.ClampMagnitude(v, 2.5f);
        Assert.Equal(2.5f, c.Length(), Eps);
        // direction preserved
        Assert.Equal(0.6f, c.X / c.Length(), Eps);
    }

    [Fact]
    public void ClampMagnitude_ShorterPassesThrough()
    {
        var v = new Vector3(1, 0, 0);
        Assert.Equal(v, MathG.ClampMagnitude(v, 5f));
    }

    [Fact]
    public void SignedAngle_PositiveAroundAxis()
    {
        // +X rotated 90deg around +Y becomes -Z (right-handed).
        var a = MathG.SignedAngle(Vector3.UnitX, -Vector3.UnitZ, Vector3.UnitY);
        Assert.Equal(MathF.PI * 0.5f, a, Eps);
    }

    [Fact]
    public void SignedAngle_NegativeReverseAxis()
    {
        // Same rotation viewed from -Y axis has opposite sign.
        var a = MathG.SignedAngle(Vector3.UnitX, -Vector3.UnitZ, -Vector3.UnitY);
        Assert.Equal(-MathF.PI * 0.5f, a, Eps);
    }

    // -------------------- LookRotation / TRS --------------------

    [Fact]
    public void LookRotation_RotatesLocalMinusZToForward()
    {
        // Pick forward = +X. Local -Z (the look direction) should
        // land on +X after applying the quaternion.
        var q = MathG.LookRotation(Vector3.UnitX, Vector3.UnitY);
        var rotated = Vector3.Transform(-Vector3.UnitZ, q);
        Assert.Equal(1f, rotated.X, Eps);
        Assert.Equal(0f, rotated.Y, Eps);
        Assert.Equal(0f, rotated.Z, Eps);
    }

    [Fact]
    public void LookRotation_KeepsUpVectorClose()
    {
        var q = MathG.LookRotation(Vector3.UnitX, Vector3.UnitY);
        var localUp = Vector3.Transform(Vector3.UnitY, q);
        Assert.Equal(0f, Vector3.Dot(localUp, Vector3.UnitX), Eps);
        Assert.True(localUp.Y > 0.99f);
    }

    [Fact]
    public void LookRotation_ParallelUpFallsBack()
    {
        // forward straight up; up parallel -> fallback path.
        var q = MathG.LookRotation(Vector3.UnitY, Vector3.UnitY);
        var rotated = Vector3.Transform(-Vector3.UnitZ, q);
        Assert.Equal(0f, rotated.X, Eps);
        Assert.Equal(1f, rotated.Y, Eps);
        Assert.Equal(0f, rotated.Z, Eps);
    }

    [Fact]
    public void LookRotation_ZeroForwardIsIdentity()
    {
        var q = MathG.LookRotation(Vector3.Zero, Vector3.UnitY);
        Assert.Equal(Quaternion.Identity, q);
    }

    [Fact]
    public void TRS_AppliesScaleThenRotateThenTranslate()
    {
        // Take a point at +X. Scale by 2 -> (2,0,0). Rotate 90deg
        // around +Y -> (0,0,-2). Translate by (1,1,1) -> (1,1,-1).
        var m = MathG.TRS(
            translation: new Vector3(1, 1, 1),
            rotation: Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI * 0.5f),
            scale: new Vector3(2, 2, 2));
        var p = Vector3.Transform(Vector3.UnitX, m);
        Assert.Equal(1f, p.X, Eps);
        Assert.Equal(1f, p.Y, Eps);
        Assert.Equal(-1f, p.Z, Eps);
    }

    [Fact]
    public void TRS_UniformScaleOverload()
    {
        var m1 = MathG.TRS(Vector3.Zero, Quaternion.Identity, new Vector3(3, 3, 3));
        var m2 = MathG.TRS(Vector3.Zero, Quaternion.Identity, 3f);
        Assert.Equal(m1, m2);
    }

    [Fact]
    public void Orbit_DefaultUnitCircleAtTimeZero()
    {
        var p = MathG.Orbit(0f);
        Assert.Equal(1f, p.X, Eps);
        Assert.Equal(0f, p.Y, Eps);
        Assert.Equal(0f, p.Z, Eps);
    }

    [Fact]
    public void Orbit_QuarterTurnLandsOnPlusZ()
    {
        // speed=1 rad/s; quarter turn at t=pi/2 -> (cos, 0, sin) = (0, 0, 1).
        var p = MathG.Orbit(MathF.PI * 0.5f, radius: 2f);
        Assert.Equal(0f, p.X, Eps);
        Assert.Equal(2f, p.Z, Eps);
    }

    [Fact]
    public void Orbit_PhaseShifts_StartingAngle()
    {
        var p = MathG.Orbit(0f, radius: 1f, speed: 1f, phase: MathF.PI * 0.5f);
        Assert.Equal(0f, p.X, Eps);
        Assert.Equal(1f, p.Z, Eps);
    }

    [Fact]
    public void Orbit2D_MatchesXZComponents()
    {
        var p3 = MathG.Orbit(0.7f, radius: 3f, speed: 2f, phase: 0.1f);
        var p2 = MathG.Orbit2D(0.7f, radius: 3f, speed: 2f, phase: 0.1f);
        Assert.Equal(p3.X, p2.X, Eps);
        Assert.Equal(p3.Z, p2.Y, Eps);
    }
}

public class EasingTests
{
    private const float Eps = 1e-4f;

    [Theory]
    [InlineData(nameof(Easing.InSine))]
    [InlineData(nameof(Easing.OutSine))]
    [InlineData(nameof(Easing.InOutSine))]
    [InlineData(nameof(Easing.InQuad))]
    [InlineData(nameof(Easing.OutQuad))]
    [InlineData(nameof(Easing.InOutQuad))]
    [InlineData(nameof(Easing.InCubic))]
    [InlineData(nameof(Easing.OutCubic))]
    [InlineData(nameof(Easing.InOutCubic))]
    [InlineData(nameof(Easing.InQuart))]
    [InlineData(nameof(Easing.OutQuart))]
    [InlineData(nameof(Easing.InOutQuart))]
    [InlineData(nameof(Easing.InQuint))]
    [InlineData(nameof(Easing.OutQuint))]
    [InlineData(nameof(Easing.InOutQuint))]
    [InlineData(nameof(Easing.InExpo))]
    [InlineData(nameof(Easing.OutExpo))]
    [InlineData(nameof(Easing.InOutExpo))]
    [InlineData(nameof(Easing.InCirc))]
    [InlineData(nameof(Easing.OutCirc))]
    [InlineData(nameof(Easing.InOutCirc))]
    [InlineData(nameof(Easing.InBack))]
    [InlineData(nameof(Easing.OutBack))]
    [InlineData(nameof(Easing.InOutBack))]
    [InlineData(nameof(Easing.InElastic))]
    [InlineData(nameof(Easing.OutElastic))]
    [InlineData(nameof(Easing.InOutElastic))]
    [InlineData(nameof(Easing.InBounce))]
    [InlineData(nameof(Easing.OutBounce))]
    [InlineData(nameof(Easing.InOutBounce))]
    public void Endpoints_AreZeroAndOne(string methodName)
    {
        var fn = (Func<float, float>)Delegate.CreateDelegate(
            typeof(Func<float, float>),
            typeof(Easing).GetMethod(methodName)!);
        Assert.Equal(0f, fn(0f), Eps);
        Assert.Equal(1f, fn(1f), Eps);
    }

    [Fact]
    public void InOut_FunctionsPassThroughHalfAtHalf()
    {
        // All standard InOut curves cross y=0.5 at x=0.5.
        Assert.Equal(0.5f, Easing.InOutSine(0.5f), Eps);
        Assert.Equal(0.5f, Easing.InOutQuad(0.5f), Eps);
        Assert.Equal(0.5f, Easing.InOutCubic(0.5f), Eps);
        Assert.Equal(0.5f, Easing.InOutQuart(0.5f), Eps);
        Assert.Equal(0.5f, Easing.InOutQuint(0.5f), Eps);
        Assert.Equal(0.5f, Easing.InOutCirc(0.5f), Eps);
    }
}
