using System.Collections.Immutable;
using Grape.Shaders.Demos;
using Grape.Shaders.Emitters;

namespace Grape.Shaders.Tests;

/// <summary>
/// Round-trips runtime-emitted SPIR-V through <see cref="SpirvReflection"/>
/// and verifies the reflector recovers the same stage, entry-point, and
/// resource counts the IR-side compiler computed for the same shader. Acts
/// as both a sanity check on the reflector and a regression guard tying the
/// two halves together.
/// </summary>
public class SpirvReflectionTests
{
    [Fact]
    public void Vertex_module_reflects_to_vertex_stage_with_main_entrypoint()
    {
        var bytes = EmitTexturedQuadVertex();
        var info = SpirvReflection.GetShaderInfo(bytes);

        Assert.Equal(ShaderKind.Vertex, info.Stage);
        Assert.Equal("main", info.Entrypoint);
    }

    [Fact]
    public void Fragment_module_reflects_to_fragment_stage_with_main_entrypoint()
    {
        var bytes = EmitTexturedQuadFragment();
        var info = SpirvReflection.GetShaderInfo(bytes);

        Assert.Equal(ShaderKind.Fragment, info.Stage);
        Assert.Equal("main", info.Entrypoint);
    }

    [Fact]
    public void Textured_quad_fragment_reports_one_sampler_zero_uniform_buffers()
    {
        // The textured quad fragment binds a Texture + Sampler pair. SDL3 GPU's
        // num_samplers convention is "1 per sampler binding," so the paired
        // texture is not double-counted.
        var bytes = EmitTexturedQuadFragment();
        var info = SpirvReflection.GetShaderInfo(bytes);

        Assert.Equal(1u, info.Resources.NumSamplers);
        Assert.Equal(0u, info.Resources.NumUniformBuffers);
        Assert.Equal(0u, info.Resources.NumStorageTextures);
        Assert.Equal(0u, info.Resources.NumStorageBuffers);
    }

    /// <summary>
    /// The strongest invariant: whatever <see cref="ShaderCompiler"/> set on
    /// each <c>StageShader</c>'s resource counts during compilation should
    /// match what <see cref="SpirvReflection"/> independently extracts from
    /// the same bytes. Runs against every built-in shader so any future
    /// shape (samplers, uniform buffers, transform matrices) is covered.
    /// </summary>
    [Theory]
    [MemberData(nameof(BuiltInShaders))]
    public void Reflected_counts_match_compiler_derived_counts(string name, Grape.ShaderSet shader)
    {
        _ = name; // displayed in test output

        var vsBytes = shader.Vertex.GetCode(ShaderFormat.Spirv);
        var vsInfo = SpirvReflection.GetShaderInfo(vsBytes.AsSpan());
        Assert.Equal(ShaderKind.Vertex, vsInfo.Stage);
        Assert.Equal(shader.Vertex.GetResources(), vsInfo.Resources);

        var fsBytes = shader.Fragment.GetCode(ShaderFormat.Spirv);
        var fsInfo = SpirvReflection.GetShaderInfo(fsBytes.AsSpan());
        Assert.Equal(ShaderKind.Fragment, fsInfo.Stage);
        Assert.Equal(shader.Fragment.GetResources(), fsInfo.Resources);
    }

    public static IEnumerable<object[]> BuiltInShaders() =>
        new[]
        {
            new object[] { nameof(Shaders.Position),                Shaders.Position },
            new object[] { nameof(Shaders.PositionTransform),       Shaders.PositionTransform },
            new object[] { nameof(Shaders.PositionTransformColor),  Shaders.PositionTransformColor },
            new object[] { nameof(Shaders.PositionColor),           Shaders.PositionColor },
            new object[] { nameof(Shaders.PositionColorTransform),  Shaders.PositionColorTransform },
            new object[] { nameof(Shaders.TexturedQuad),            Shaders.TexturedQuad },
            new object[] { nameof(Shaders.TexturedQuadWithMatrix),  Shaders.TexturedQuadWithMatrix },
        };

    [Fact]
    public void LoadFromFile_produces_StageShader_matching_the_original()
    {
        var bytes = EmitTexturedQuadFragment();
        var loaded = new Shader(
            ShaderKind.Fragment,
            ShaderFormat.Spirv,
            System.Collections.Immutable.ImmutableArray.Create(bytes));

        Assert.Equal(ShaderKind.Fragment, loaded.Kind);
        Assert.Equal("main", loaded.Entrypoint);
        Assert.Equal(1u, loaded.GetResources().NumSamplers);
        Assert.Equal(bytes.Length, loaded.GetCode(ShaderFormat.Spirv).Length);
    }

    [Fact]
    public void Save_then_LoadFromFile_round_trips_via_disk()
    {
        var original = Shaders.TexturedQuadWithMatrix.Vertex;
        var originalBytes = original.GetCode(ShaderFormat.Spirv);

        var path = Path.Combine(Path.GetTempPath(),
            $"grape-spirv-test-{Guid.NewGuid():N}.spv");
        try
        {
            original.Save(path, ShaderFormat.Spirv);
            var loaded = Shader.Load(path, original.Kind, ShaderFormat.Spirv);

            Assert.Equal(original.Kind, loaded.Kind);
            Assert.Equal(original.Entrypoint, loaded.Entrypoint);
            Assert.Equal(original.GetResources(), loaded.GetResources());
            var loadedBytes = loaded.GetCode(ShaderFormat.Spirv);
            Assert.Equal(originalBytes.Length, loadedBytes.Length);
            Assert.True(originalBytes.AsSpan().SequenceEqual(loadedBytes.AsSpan()));
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void Rejects_non_spirv_bytes()
    {
        var notSpirv = new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07,
                                    0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07,
                                    0x00, 0x00, 0x00, 0x00 };
        Assert.Throws<ArgumentException>(() => SpirvReflection.GetShaderInfo(notSpirv));
    }

    [Fact]
    public void Rejects_truncated_module()
    {
        var tooShort = new byte[] { 0x03, 0x02, 0x23, 0x07, 0x00 };
        Assert.Throws<ArgumentException>(() => SpirvReflection.GetShaderInfo(tooShort));
    }

    // ---- Helpers ----

    private static byte[] EmitTexturedQuadVertex()
    {
        var laidOut = BindAndLayout();
        var output = new SpvEmitter().Emit(laidOut);
        Assert.NotNull(output.Vertex);
        return output.Vertex!;
    }

    private static byte[] EmitTexturedQuadFragment()
    {
        var laidOut = BindAndLayout();
        var output = new SpvEmitter().Emit(laidOut);
        Assert.NotNull(output.Fragment);
        return output.Fragment!;
    }

    private static ShaderSet BindAndLayout()
    {
        var types  = new StandardShaderTypeSystem();
        var set    = TexturedQuadDemo.Build(types);
        var (bound, diags) = new ShaderBinder(types).Bind(set);
        Assert.Empty(diags);
        return ShaderLayout.AssignLayout(bound);
    }
}
