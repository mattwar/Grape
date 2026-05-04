// Placed outside the Grape.Shaders namespace tree so the *type*
// `Grape.Shaders` (defined in the Grape assembly) is resolvable here. From
// anywhere inside namespace Grape.Shaders.* the same identifier binds to
// the namespace instead, hiding the type entirely; this is purely a
// namespace/type aliasing quirk, not a real ambiguity.
namespace Grape.Tests.Hlsl;

using Grape;

/// <summary>
/// Smoke-tests the runtime HLSL -> shadercross compilation path used by
/// every shader exposed on <c>Grape.Shaders</c>. Each set's vertex and
/// fragment stages are asked to produce SPIR-V (which forces the DXC
/// front end to run) and to report their reflected resource counts, then
/// the SPIR-V is independently re-reflected to confirm the bytes are a
/// valid module of the expected stage.
/// </summary>
/// <remarks>
/// Doing this for every built-in shader at once both proves the new
/// HLSL-source pipeline works and locks in resource-count expectations for
/// each shader, so a future change to one shader's bindings won't silently
/// break a renderer that relies on the previous shape.
/// </remarks>
public class BuiltInShadersHlslTests
{
    public static IEnumerable<object[]> All() =>
        new[]
        {
            new object[] { nameof(Shaders.PositionColor),          Shaders.PositionColor,          0u, 0u },
            new object[] { nameof(Shaders.PositionColorTransform), Shaders.PositionColorTransform, 1u, 0u },
            new object[] { nameof(Shaders.TexturedQuad),           Shaders.TexturedQuad,           0u, 1u },
            new object[] { nameof(Shaders.TexturedQuadWithMatrix), Shaders.TexturedQuadWithMatrix, 1u, 1u },
        };

    [Theory]
    [MemberData(nameof(All))]
    public void Compiles_via_shadercross_and_reflects_expected_resources(
        string name,
        ShaderSet set,
        uint expectedVertexUniformBuffers,
        uint expectedFragmentSamplers)
    {
        _ = name;

        // Touching GetCode(Spirv) drives the HLSL -> DXC -> SPIR-V path.
        var vsBytes = set.Vertex.GetCode(ShaderFormat.Spirv);
        var fsBytes = set.Fragment.GetCode(ShaderFormat.Spirv);
        Assert.False(vsBytes.IsDefaultOrEmpty);
        Assert.False(fsBytes.IsDefaultOrEmpty);

        // Reflected counts via Shader.GetResources should match what the
        // hand-baked precompiled shaders advertised.
        var vRes = set.Vertex.GetResources();
        var fRes = set.Fragment.GetResources();
        Assert.Equal(expectedVertexUniformBuffers, vRes.NumUniformBuffers);
        Assert.Equal(expectedFragmentSamplers, fRes.NumSamplers);

        // Independent reflection on the produced SPIR-V should agree.
        var vsInfo = SpirvReflection.GetShaderInfo(vsBytes.AsSpan());
        var fsInfo = SpirvReflection.GetShaderInfo(fsBytes.AsSpan());
        Assert.Equal(ShaderKind.Vertex, vsInfo.Stage);
        Assert.Equal(ShaderKind.Fragment, fsInfo.Stage);
        Assert.Equal(vRes, vsInfo.Resources);
        Assert.Equal(fRes, fsInfo.Resources);
    }
}
