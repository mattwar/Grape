using System.Numerics;
using Blitter.Bits;

namespace Blitter.Tests;

public class MatrixExtensionsTests
{
    [Fact]
    public void Matrix4x4_Chain_MatchesMultiplication()
    {
        var chained = Matrix4x4.Identity
            .Scale(2f)
            .RotateZ(0.4f)
            .Translate(1f, 2f, 3f);

        var manual =
            Matrix4x4.Identity *
            Matrix4x4.CreateScale(2f) *
            Matrix4x4.CreateRotationZ(0.4f) *
            Matrix4x4.CreateTranslation(1f, 2f, 3f);

        Assert.Equal(manual, chained);
    }

    [Fact]
    public void Matrix4x4_Chain_AppliesInOrder()
    {
        // Apply-order: scale first, then translate. Translation should
        // not be scaled because it composes after.
        var transform = Matrix4x4.Identity
            .Scale(2f)
            .Translate(10f, 0f, 0f);

        var p = Vector3.Transform(Vector3.UnitX, transform);

        // UnitX scaled by 2 -> (2,0,0); then translated by (10,0,0) -> (12,0,0).
        Assert.Equal(new Vector3(12f, 0f, 0f), p);
    }

    [Fact]
    public void Matrix3x2_Chain_MatchesMultiplication()
    {
        var chained = Matrix3x2.Identity
            .Scale(3f)
            .Rotate(0.5f)
            .Translate(4f, 5f);

        var manual =
            Matrix3x2.Identity *
            Matrix3x2.CreateScale(3f) *
            Matrix3x2.CreateRotation(0.5f) *
            Matrix3x2.CreateTranslation(4f, 5f);

        Assert.Equal(manual, chained);
    }
}
