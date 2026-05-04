using System.Collections.Immutable;
using System.Numerics;
using static Grape.Shaders.ShaderFactory;

namespace Grape.Shaders;

/// <summary>
/// Runtime-compiled equivalents of <see cref="BuiltInShaders"/>. Each shader
/// is described as an expression tree and lowered to SPIR-V on first access
/// via <see cref="ShaderCompiler"/> -- no precompiled bytecode involved.
/// </summary>
/// <remarks>
/// Resource bindings are assigned to match SDL3 GPU's SPIR-V conventions so
/// the produced shaders drop in alongside the precompiled built-ins: vertex
/// uniform buffers go in descriptor set 1; fragment textures and samplers
/// share descriptor set 2.
/// </remarks>
public static class Shaders
{
    private static readonly ShaderTypeSystem _types = new StandardShaderTypeSystem();
    private static readonly ShaderCompiler _compiler = new(_types);

    private static Shader<ColorVertex3D>? _positionColor;
    private static Shader<ColorVertex3D, Matrix4x4>? _positionColorTransform;
    private static Shader<TextureVertex3D>? _texturedQuad;
    private static Shader<TextureVertex3D, Matrix4x4>? _texturedQuadWithMatrix;
    private static Shader<Vertex3D>? _position;
    private static Shader<Vertex3D, Matrix4x4>? _positionTransform;
    private static Shader<Vertex3D, PositionTransformColorArgs>? _positionTransformColor;

    /// <summary>Position-only vertices, no transform; emits a solid white pixel.</summary>
    public static Shader<Vertex3D> Position =>
        _position ??= BuildPosition();

    /// <summary>Position-only vertices, transformed by a per-draw 4x4 matrix; emits a solid white pixel.</summary>
    public static Shader<Vertex3D, Matrix4x4> PositionTransform =>
        _positionTransform ??= BuildPositionTransform();

    /// <summary>
    /// Position-only vertices, transformed by a per-draw 4x4 matrix, with a
    /// per-draw fragment color. Pair with
    /// <see cref="PositionTransformColorArgs"/>.
    /// </summary>
    public static Shader<Vertex3D, PositionTransformColorArgs> PositionTransformColor =>
        _positionTransformColor ??= BuildPositionTransformColor();

    /// <summary>Position + per-vertex color, no transform.</summary>
    public static Shader<ColorVertex3D> PositionColor =>
        _positionColor ??= BuildPositionColor();

    /// <summary>Position + per-vertex color, transformed by a per-draw 4x4 matrix.</summary>
    public static Shader<ColorVertex3D, Matrix4x4> PositionColorTransform =>
        _positionColorTransform ??= BuildPositionColorTransform();

    /// <summary>Position + UV, no transform; samples the bound texture.</summary>
    public static Shader<TextureVertex3D> TexturedQuad =>
        _texturedQuad ??= BuildTexturedQuad();

    /// <summary>Position + UV, transformed by a per-draw 4x4 matrix; samples the bound texture.</summary>
    public static Shader<TextureVertex3D, Matrix4x4> TexturedQuadWithMatrix =>
        _texturedQuadWithMatrix ??= BuildTexturedQuadWithMatrix();

    /// <summary>Sugar for <see cref="ShaderCompiler.Compile{TVertex}"/> using the shared default compiler.</summary>
    public static CompileResult<TVertex> Compile<TVertex>(
        ShaderSet set,
        ShaderVertexLayout ShaderVertexLayout,
        ShaderResourceCounts vertexResources = default,
        ShaderResourceCounts fragmentResources = default)
        where TVertex : unmanaged
        => _compiler.Compile<TVertex>(set, ShaderVertexLayout, vertexResources, fragmentResources);

    /// <summary>Sugar for <see cref="ShaderCompiler.Compile{TVertex,TArgs}"/> using the shared default compiler.</summary>
    public static CompileResult<TVertex, TArgs> Compile<TVertex, TArgs>(
        ShaderSet set,
        ShaderVertexLayout ShaderVertexLayout,
        ShaderArgsLayout argsLayout)
        where TVertex : unmanaged
        where TArgs : unmanaged
        => _compiler.Compile<TVertex, TArgs>(set, ShaderVertexLayout, argsLayout);

    /// <summary>
    /// A single-element <see cref="ShaderArgsLayout"/> describing a 4x4 matrix
    /// at vertex slot 0 -- the convention used by the built-in transform
    /// shaders.
    /// </summary>
    private static readonly ShaderArgsLayout TransformLayout = new(
        new ShaderArgElement(ShaderArgStage.Vertex, 0, ShaderArgKind.Matrix4x4));

    private static Shader<Vertex3D> BuildPosition()
    {
        var (set, vRes) = ComposePosition(transform: false);
        return CompileOrThrow<Vertex3D>(set, VertexOnlyMesh.ShaderVertexLayout, vRes, default);
    }

    private static Shader<Vertex3D, Matrix4x4> BuildPositionTransform()
    {
        var (set, _) = ComposePosition(transform: true);
        return CompileTransformOrThrow<Vertex3D>(set, VertexOnlyMesh.ShaderVertexLayout);
    }

    private static (ShaderSet Set, ShaderResourceCounts VRes) ComposePosition(bool transform)
    {
        var vec3 = _types.GetVector(ShaderTypeSystem.Float, 3);
        var vec4 = _types.GetVector(ShaderTypeSystem.Float, 4);
        var mat4 = _types.GetMatrix(ShaderTypeSystem.Float, 4, 4);

        var aPos  = VertexInput("a_pos", vec3);
        var glPos = Builtin(ShaderBuiltin.Position, ShaderGlobalKind.StageOutput, vec4);
        var uMvp  = transform ? Uniform("u_mvp", mat4).WithBinding(1, 0) : null;

        ShaderExpression positionExpr = transform
            ? MatMul(Ref(uMvp!), Construct(vec4, Ref(aPos), Const(1.0f)))
            : Construct(vec4, Ref(aPos), Const(1.0f));

        var vsGlobals = uMvp is null
            ? ImmutableArray.Create(aPos, glPos)
            : ImmutableArray.Create(aPos, glPos, uMvp);

        var vs = Stage(
            ShaderStageKind.Vertex,
            vsGlobals,
            ImmutableArray<ShaderFunction>.Empty,
            Assign(Ref(glPos), positionExpr));

        var oColor = StageOutput("o_color", vec4);
        var fs = Stage(
            ShaderStageKind.Fragment,
            ImmutableArray.Create(oColor),
            ImmutableArray<ShaderFunction>.Empty,
            Assign(
                Ref(oColor),
                Construct(vec4, Const(1.0f), Const(1.0f), Const(1.0f), Const(1.0f))));

        var vertexResources = transform
            ? new ShaderResourceCounts(NumUniformBuffers: 1)
            : default;

        return (Set(vs, fs), vertexResources);
    }

    private static Shader<ColorVertex3D> BuildPositionColor()
    {
        var (set, vRes) = ComposePositionColor(transform: false);
        return CompileOrThrow<ColorVertex3D>(set, ColoredMesh.ShaderVertexLayout, vRes, default);
    }

    private static Shader<ColorVertex3D, Matrix4x4> BuildPositionColorTransform()
    {
        var (set, _) = ComposePositionColor(transform: true);
        return CompileTransformOrThrow<ColorVertex3D>(set, ColoredMesh.ShaderVertexLayout);
    }

    private static (ShaderSet Set, ShaderResourceCounts VRes) ComposePositionColor(bool transform)
    {
        var vec3 = _types.GetVector(ShaderTypeSystem.Float, 3);
        var vec4 = _types.GetVector(ShaderTypeSystem.Float, 4);
        var mat4 = _types.GetMatrix(ShaderTypeSystem.Float, 4, 4);

        var aPos    = VertexInput("a_pos",   vec3);
        var aColor  = VertexInput("a_color", vec4);
        var vColor  = StageOutput("v_color", vec4);
        var glPos   = Builtin(ShaderBuiltin.Position, ShaderGlobalKind.StageOutput, vec4);
        var uMvp    = transform ? Uniform("u_mvp", mat4).WithBinding(1, 0) : null;

        ShaderExpression positionExpr = transform
            ? MatMul(Ref(uMvp!), Construct(vec4, Ref(aPos), Const(1.0f)))
            : Construct(vec4, Ref(aPos), Const(1.0f));

        var vsGlobals = uMvp is null
            ? ImmutableArray.Create(aPos, aColor, vColor, glPos)
            : ImmutableArray.Create(aPos, aColor, vColor, glPos, uMvp);

        var vs = Stage(
            ShaderStageKind.Vertex,
            vsGlobals,
            ImmutableArray<ShaderFunction>.Empty,
            Block(
                Assign(Ref(glPos), positionExpr),
                Assign(Ref(vColor), Ref(aColor))));

        var fs = SolidColorFragmentStage(vec4);

        var vertexResources = transform
            ? new ShaderResourceCounts(NumUniformBuffers: 1)
            : default;

        return (Set(vs, fs), vertexResources);
    }

    private static Shader<TextureVertex3D> BuildTexturedQuad()
    {
        var (set, vRes, fRes) = ComposeTexturedQuad(transform: false);
        return CompileOrThrow<TextureVertex3D>(set, TexturedMesh.ShaderVertexLayout, vRes, fRes);
    }

    private static Shader<TextureVertex3D, Matrix4x4> BuildTexturedQuadWithMatrix()
    {
        var (set, _, _) = ComposeTexturedQuad(transform: true);
        return CompileTransformOrThrow<TextureVertex3D>(set, TexturedMesh.ShaderVertexLayout);
    }

    private static (ShaderSet Set, ShaderResourceCounts VRes, ShaderResourceCounts FRes) ComposeTexturedQuad(bool transform)
    {
        var vec2 = _types.GetVector(ShaderTypeSystem.Float, 2);
        var vec3 = _types.GetVector(ShaderTypeSystem.Float, 3);
        var vec4 = _types.GetVector(ShaderTypeSystem.Float, 4);
        var mat4 = _types.GetMatrix(ShaderTypeSystem.Float, 4, 4);

        var aPos  = VertexInput("a_pos", vec3);
        var aUv   = VertexInput("a_uv",  vec2);
        var vUv   = StageOutput("v_uv",  vec2);
        var glPos = Builtin(ShaderBuiltin.Position, ShaderGlobalKind.StageOutput, vec4);
        var uMvp  = transform ? Uniform("u_mvp", mat4).WithBinding(1, 0) : null;

        ShaderExpression positionExpr = transform
            ? MatMul(Ref(uMvp!), Construct(vec4, Ref(aPos), Const(1.0f)))
            : Construct(vec4, Ref(aPos), Const(1.0f));

        var vsGlobals = uMvp is null
            ? ImmutableArray.Create(aPos, aUv, vUv, glPos)
            : ImmutableArray.Create(aPos, aUv, vUv, glPos, uMvp);

        var vs = Stage(
            ShaderStageKind.Vertex,
            vsGlobals,
            ImmutableArray<ShaderFunction>.Empty,
            Block(
                Assign(Ref(glPos), positionExpr),
                Assign(Ref(vUv), Ref(aUv))));

        var fUv    = StageInput("v_uv", vec2);
        var oColor = StageOutput("o_color", vec4);
        var uTex   = Texture("u_tex", ShaderTypeSystem.Texture2D).WithBinding(2, 0);
        var uSamp  = Sampler("u_samp").WithBinding(2, 1);

        var fs = Stage(
            ShaderStageKind.Fragment,
            ImmutableArray.Create(fUv, oColor, uTex, uSamp),
            ImmutableArray<ShaderFunction>.Empty,
            Assign(
                Ref(oColor),
                Sample(Ref(uTex), Ref(uSamp), Ref(fUv))));

        var vertexResources = transform
            ? new ShaderResourceCounts(NumUniformBuffers: 1)
            : default;

        return (Set(vs, fs), vertexResources, new ShaderResourceCounts(NumSamplers: 1));
    }

    /// <summary>Solid-color fragment stage: passes the interpolated vertex color through.</summary>
    private static ShaderStage SolidColorFragmentStage(VectorType vec4)
    {
        return Stage(
            ShaderStageKind.Fragment,
            StageInput("v_color", vec4),
            StageOutput("o_color", vec4),
            (fColor, oColor) => Assign(Ref(oColor), Ref(fColor))
            );
    }

    private static Shader<TVertex> CompileOrThrow<TVertex>(
        ShaderSet set,
        ShaderVertexLayout ShaderVertexLayout,
        ShaderResourceCounts vertexResources,
        ShaderResourceCounts fragmentResources)
        where TVertex : unmanaged
    {
        var result = _compiler.Compile<TVertex>(set, ShaderVertexLayout, vertexResources, fragmentResources);
        if (result.Shader is null)
            throw new InvalidOperationException(
                "Built-in shader IR failed to compile: " + string.Join("; ", result.Diagnostics));
        return result.Shader;
    }

    private static Shader<TVertex, Matrix4x4> CompileTransformOrThrow<TVertex>(
        ShaderSet set,
        ShaderVertexLayout ShaderVertexLayout)
        where TVertex : unmanaged
    {
        // The typed-args Compile derives NumUniformBuffers from the layout
        // and NumSamplers by counting Sampler globals in the IR, so no
        // ShaderResourceCounts need to be supplied here.
        var result = _compiler.Compile<TVertex, Matrix4x4>(set, ShaderVertexLayout, TransformLayout);
        if (result.Shader is null)
            throw new InvalidOperationException(
                "Built-in shader IR failed to compile: " + string.Join("; ", result.Diagnostics));
        return result.Shader;
    }

    private static Shader<Vertex3D, PositionTransformColorArgs> BuildPositionTransformColor()
    {
        var vec3 = _types.GetVector(ShaderTypeSystem.Float, 3);
        var vec4 = _types.GetVector(ShaderTypeSystem.Float, 4);
        var mat4 = _types.GetMatrix(ShaderTypeSystem.Float, 4, 4);

        var aPos  = VertexInput("a_pos", vec3);
        var glPos = Builtin(ShaderBuiltin.Position, ShaderGlobalKind.StageOutput, vec4);
        var uMvp  = Uniform("u_mvp", mat4).WithBinding(1, 0);

        var vs = Stage(
            ShaderStageKind.Vertex,
            ImmutableArray.Create(aPos, glPos, uMvp),
            ImmutableArray<ShaderFunction>.Empty,
            Assign(Ref(glPos),
                MatMul(Ref(uMvp), Construct(vec4, Ref(aPos), Const(1.0f)))));

        var oColor = StageOutput("o_color", vec4);
        var uColor = Uniform("u_color", vec4).WithBinding(3, 0);

        var fs = Stage(
            ShaderStageKind.Fragment,
            ImmutableArray.Create(oColor, uColor),
            ImmutableArray<ShaderFunction>.Empty,
            Assign(Ref(oColor), Ref(uColor)));

        var argsLayout = new ShaderArgsLayout(
            new ShaderArgElement(ShaderArgStage.Vertex,   0, ShaderArgKind.Matrix4x4),
            new ShaderArgElement(ShaderArgStage.Fragment, 0, ShaderArgKind.Float4));

        var result = _compiler.Compile<Vertex3D, PositionTransformColorArgs>(
            Set(vs, fs),
            VertexOnlyMesh.ShaderVertexLayout,
            argsLayout);

        if (result.Shader is null)
            throw new InvalidOperationException(
                "Built-in shader IR failed to compile: " + string.Join("; ", result.Diagnostics));
        return result.Shader;
    }
}

/// <summary>
/// Per-draw arguments supplied to <see cref="Shaders.PositionTransformColor"/>:
/// a model-view-projection matrix (vertex slot 0) and a fragment color
/// (fragment slot 0).
/// </summary>
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
public struct PositionTransformColorArgs
{
    public System.Numerics.Matrix4x4 Mvp;
    public System.Numerics.Vector4   Color;
}
