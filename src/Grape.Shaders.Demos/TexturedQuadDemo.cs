using static Grape.Shaders.ShaderFactory;

namespace Grape.Shaders.Demos;

/// <summary>
/// A textured-quad demo: vertex transforms a position by a uniform MVP matrix and
/// passes a UV through; fragment samples a 2D texture+sampler. Exercises the
/// interesting intersections: a built-in (gl_Position), a varying matched by
/// name across stages, a uniform, a texture/sampler pair, and matrix*vector
/// MatMul.
/// </summary>
public static class TexturedQuadDemo
{
    public static ShaderSet Build(ShaderTypeSystem types)
    {
        var vec2 = types.GetVector(ShaderTypeSystem.Float, 2);
        var vec3 = types.GetVector(ShaderTypeSystem.Float, 3);
        var vec4 = types.GetVector(ShaderTypeSystem.Float, 4);
        var mat4 = types.GetMatrix(ShaderTypeSystem.Float, 4, 4);

        // ---- Vertex stage ----
        var aPos     = VertexInput("a_pos", vec3);
        var aUv      = VertexInput("a_uv",  vec2);
        var vUv      = StageOutput("v_uv", vec2);
        var glPos    = Builtin(ShaderBuiltin.Position, ShaderGlobalKind.StageOutput, vec4);
        var uMvp     = Uniform("u_mvp", mat4);

        var vsBody = Block(
            Assign(
                Ref(glPos),
                MatMul(
                    Ref(uMvp),
                    Construct(vec4, Ref(aPos), Const(1.0f)))),
            Assign(Ref(vUv), Ref(aUv)));

        var vs = Stage(
            ShaderStageKind.Vertex,
            [aPos, aUv, vUv, glPos, uMvp],
            [],
            vsBody);

        // ---- Fragment stage ----
        var fUv      = StageInput("v_uv", vec2);
        var oColor   = StageOutput("o_color", vec4);
        var uTex     = Texture("u_tex", ShaderTypeSystem.Texture2D);
        var uSamp    = Sampler("u_samp");

        var fsBody = Assign(
            Ref(oColor),
            Sample(
                Ref(uTex),
                Ref(uSamp),
                Ref(fUv)));

        var fs = Stage(
            ShaderStageKind.Fragment,
            [fUv, oColor, uTex, uSamp],
            [],
            fsBody);

        return Set(vs, fs);
    }
}
