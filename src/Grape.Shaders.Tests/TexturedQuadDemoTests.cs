using System.Collections.Immutable;
using Grape.Shaders.Demos;
using Grape.Shaders.Emitters;

namespace Grape.Shaders.Tests;

/// <summary>
/// End-to-end smoke test covering Build → Bind → Layout → Emit on a
/// representative vertex+fragment shader set.
/// </summary>
public class TexturedQuadDemoTests
{
    [Fact]
    public void Factory_output_is_unbound()
    {
        var set = TexturedQuadDemo.Build(new StandardShaderTypeSystem());
        Assert.True(set.IsUnbound);
    }

    [Fact]
    public void Binder_resolves_module_with_no_diagnostics()
    {
        var types  = new StandardShaderTypeSystem();
        var set    = TexturedQuadDemo.Build(types);

        var (bound, diagnostics) = new ShaderBinder(types).Bind(set);

        Assert.False(bound.IsUnbound);
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Layout_pass_assigns_locations_and_bindings()
    {
        var (_, _, laidOut) = BuildBindLayout();

        var v = laidOut.Vertex!;
        Assert.Equal(0, GlobalByName(v, "a_pos").Location);
        Assert.Equal(1, GlobalByName(v, "a_uv").Location);
        Assert.Equal(0, GlobalByName(v, "v_uv").Location);

        var uMvp = GlobalByName(v, "u_mvp");
        Assert.Equal(0, uMvp.BindingSet);
        Assert.Equal(0, uMvp.BindingSlot);

        var f = laidOut.Fragment!;
        // Fragment input v_uv is matched to vertex output v_uv by name -> same location.
        Assert.Equal(GlobalByName(v, "v_uv").Location, GlobalByName(f, "v_uv").Location);
        Assert.Equal(0, GlobalByName(f, "o_color").Location);

        var uTex  = GlobalByName(f, "u_tex");
        var uSamp = GlobalByName(f, "u_samp");
        Assert.Equal(1, uTex.BindingSlot);
        Assert.Equal(2, uSamp.BindingSlot);
        Assert.Equal(0, uTex.BindingSet);
        Assert.Equal(0, uSamp.BindingSet);
    }

    [Fact]
    public void Builtin_globals_receive_no_layout()
    {
        var (_, _, laidOut) = BuildBindLayout();
        var glPos = GlobalByName(laidOut.Vertex!, "Position");
        Assert.Null(glPos.Location);
        Assert.Null(glPos.BindingSet);
        Assert.Null(glPos.BindingSlot);
    }

    [Fact]
    public void Glsl_emit_includes_expected_declarations_and_body()
    {
        var (_, _, laidOut) = BuildBindLayout();
        var output = new GlslEmitter().Emit(laidOut);

        Assert.NotNull(output.Vertex);
        Assert.NotNull(output.Fragment);

        var vs = output.Vertex!;
        Assert.Contains("#version 450", vs);
        Assert.Contains("layout(location = 0) in vec3 a_pos;", vs);
        Assert.Contains("layout(location = 1) in vec2 a_uv;", vs);
        Assert.Contains("layout(location = 0) out vec2 v_uv;", vs);
        Assert.Contains("layout(set = 0, binding = 0) uniform _ub_u_mvp { mat4 u_mvp; };", vs);
        Assert.Contains("gl_Position = (u_mvp * vec4(a_pos, 1.0));", vs);
        Assert.Contains("v_uv = a_uv;", vs);

        var fs = output.Fragment!;
        Assert.Contains("layout(location = 0) in vec2 v_uv;", fs);
        Assert.Contains("layout(location = 0) out vec4 o_color;", fs);
        Assert.Contains("layout(set = 0, binding = 1) uniform texture2D u_tex;", fs);
        Assert.Contains("layout(set = 0, binding = 2) uniform sampler u_samp;", fs);
        Assert.Contains("o_color = texture(sampler2D(u_tex, u_samp), v_uv);", fs);
    }

    [Fact]
    public void Emitter_rejects_unbound_module()
    {
        var set = TexturedQuadDemo.Build(new StandardShaderTypeSystem());
        Assert.Throws<InvalidOperationException>(() => new GlslEmitter().Emit(set));
    }

    // ---- Helpers ----

    private static (ShaderSet unbound, ShaderSet bound, ShaderSet laidOut) BuildBindLayout()
    {
        var types  = new StandardShaderTypeSystem();
        var set    = TexturedQuadDemo.Build(types);
        var (bound, diags) = new ShaderBinder(types).Bind(set);
        Assert.Empty(diags);
        var laidOut = ShaderLayout.AssignLayout(bound);
        return (set, bound, laidOut);
    }

    private static ShaderGlobal GlobalByName(ShaderStage stage, string name)
    {
        foreach (var g in stage.Globals)
            if (g.Name == name) return g;
        throw new Xunit.Sdk.XunitException($"Global '{name}' not found on stage {stage.Stage}.");
    }
}
