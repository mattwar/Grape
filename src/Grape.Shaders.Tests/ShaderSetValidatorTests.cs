using Grape.Shaders.Demos;
using static Grape.Shaders.ShaderFactory;

namespace Grape.Shaders.Tests;

/// <summary>
/// Cross-stage validation: the textured-quad demo should pass cleanly,
/// and synthetic mismatches must be reported.
/// </summary>
public class ShaderSetValidatorTests
{
    [Fact]
    public void Textured_quad_demo_validates_cleanly()
    {
        var types = new StandardShaderTypeSystem();
        var set   = TexturedQuadDemo.Build(types);
        var (bound, _) = new ShaderBinder(types).Bind(set);

        var (validated, diagnostics) = ShaderSetValidator.Validate(bound);

        Assert.False(validated.ContainsDiagnostics);
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Reports_fragment_input_with_no_matching_vertex_output()
    {
        var types = new StandardShaderTypeSystem();
        var vec2  = types.GetVector(ShaderTypeSystem.Float, 2);
        var vec4  = types.GetVector(ShaderTypeSystem.Float, 4);

        // Vertex stage writes only gl_Position; no v_uv output.
        var glPos = Builtin(ShaderBuiltin.Position, ShaderGlobalKind.StageOutput, vec4);
        var vs = Stage(ShaderStageKind.Vertex, [glPos], [],
            Assign(Ref(glPos), Construct(vec4, Const(0f), Const(0f), Const(0f), Const(1f))));

        // Fragment stage reads v_uv that the vertex stage doesn't produce.
        var vUv    = StageInput("v_uv", vec2);
        var oColor = StageOutput("o_color", vec4);
        var fs = Stage(ShaderStageKind.Fragment, [vUv, oColor], [],
            Assign(Ref(oColor), Construct(vec4, Ref(vUv), Const(0f), Const(1f))));

        var (bound, _) = new ShaderBinder(types).Bind(Set(vs, fs));
        var (_, diagnostics) = ShaderSetValidator.Validate(bound);

        Assert.Contains(diagnostics, d => d.Code == "SH0200" && d.Message.Contains("v_uv"));
    }

    [Fact]
    public void Reports_varying_type_mismatch()
    {
        var types = new StandardShaderTypeSystem();
        var vec2  = types.GetVector(ShaderTypeSystem.Float, 2);
        var vec3  = types.GetVector(ShaderTypeSystem.Float, 3);
        var vec4  = types.GetVector(ShaderTypeSystem.Float, 4);

        // Vertex output v_uv is vec3; fragment input v_uv is vec2.
        var glPos  = Builtin(ShaderBuiltin.Position, ShaderGlobalKind.StageOutput, vec4);
        var vUvOut = StageOutput("v_uv", vec3);
        var vs = Stage(ShaderStageKind.Vertex, [glPos, vUvOut], [],
            Block(
                Assign(Ref(glPos),  Construct(vec4, Const(0f), Const(0f), Const(0f), Const(1f))),
                Assign(Ref(vUvOut), Construct(vec3, Const(0f), Const(0f), Const(0f)))));

        var vUvIn  = StageInput("v_uv", vec2);
        var oColor = StageOutput("o_color", vec4);
        var fs = Stage(ShaderStageKind.Fragment, [vUvIn, oColor], [],
            Assign(Ref(oColor), Construct(vec4, Ref(vUvIn), Const(0f), Const(1f))));

        var (bound, _) = new ShaderBinder(types).Bind(Set(vs, fs));
        var (_, diagnostics) = ShaderSetValidator.Validate(bound);

        Assert.Contains(diagnostics, d => d.Code == "SH0201" && d.Message.Contains("v_uv"));
    }

    [Fact]
    public void Reports_resource_kind_conflict_across_stages()
    {
        var types = new StandardShaderTypeSystem();
        var vec4  = types.GetVector(ShaderTypeSystem.Float, 4);

        // Vertex declares u_x as a uniform; fragment declares u_x as a texture.
        var glPos      = Builtin(ShaderBuiltin.Position, ShaderGlobalKind.StageOutput, vec4);
        var uxUniform  = Uniform("u_x", vec4);
        var vs = Stage(ShaderStageKind.Vertex, [glPos, uxUniform], [],
            Assign(Ref(glPos), Ref(uxUniform)));

        var oColor    = StageOutput("o_color", vec4);
        var uxTexture = Texture("u_x", ShaderTypeSystem.Texture2D);
        var uSamp     = Sampler("u_samp");
        var fs = Stage(ShaderStageKind.Fragment, [oColor, uxTexture, uSamp], [],
            Assign(Ref(oColor),
                Sample(Ref(uxTexture), Ref(uSamp),
                    Construct(types.GetVector(ShaderTypeSystem.Float, 2), Const(0f), Const(0f)))));

        var (bound, _) = new ShaderBinder(types).Bind(Set(vs, fs));
        var (_, diagnostics) = ShaderSetValidator.Validate(bound);

        Assert.Contains(diagnostics, d => d.Code == "SH0202" && d.Message.Contains("u_x"));
    }
}
