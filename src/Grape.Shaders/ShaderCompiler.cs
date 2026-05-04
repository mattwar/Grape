using System.Collections.Immutable;
using Grape.Shaders.Emitters;

namespace Grape.Shaders;

/// <summary>
/// Outcome of <see cref="ShaderCompiler.Compile{TVertex}"/>: the compiled
/// <see cref="Shader{TVertex}"/> when compilation succeeded, plus every
/// diagnostic recorded along the way.
/// </summary>
public sealed class CompileResult<TVertex>(
    Shader<TVertex>? shader,
    ImmutableList<ShaderDiagnostic> diagnostics)
    where TVertex : unmanaged
{
    public Shader<TVertex>? Shader { get; } = shader;
    public ImmutableList<ShaderDiagnostic> Diagnostics { get; } = diagnostics;

    /// <summary>True if any diagnostic has <see cref="ShaderDiagnosticSeverity.Error"/>.</summary>
    public bool HasErrors
    {
        get
        {
            foreach (var d in Diagnostics)
                if (d.Severity == ShaderDiagnosticSeverity.Error) return true;
            return false;
        }
    }

    public void Deconstruct(out Shader<TVertex>? shader, out ImmutableList<ShaderDiagnostic> diagnostics)
    {
        shader = Shader;
        diagnostics = Diagnostics;
    }
}

/// <summary>
/// Outcome of <see cref="ShaderCompiler.Compile{TVertex,TArgs}"/>: the
/// compiled <see cref="Shader{TVertex,TArgs}"/> when compilation succeeded,
/// plus every diagnostic recorded along the way.
/// </summary>
public sealed class CompileResult<TVertex, TArgs>(
    Shader<TVertex, TArgs>? shader,
    ImmutableList<ShaderDiagnostic> diagnostics)
    where TVertex : unmanaged
    where TArgs : unmanaged
{
    public Shader<TVertex, TArgs>? Shader { get; } = shader;
    public ImmutableList<ShaderDiagnostic> Diagnostics { get; } = diagnostics;

    public bool HasErrors
    {
        get
        {
            foreach (var d in Diagnostics)
                if (d.Severity == ShaderDiagnosticSeverity.Error) return true;
            return false;
        }
    }

    public void Deconstruct(out Shader<TVertex, TArgs>? shader, out ImmutableList<ShaderDiagnostic> diagnostics)
    {
        shader = Shader;
        diagnostics = Diagnostics;
    }
}

/// <summary>
/// Drives the full compile pipeline -- bind, layout, emit -- and packages the
/// resulting bytecode as a <see cref="Shader{TVertex}"/> ready to hand to a
/// renderer. Targets SPIR-V; emission is device-independent.
/// </summary>
public sealed class ShaderCompiler
{
    private readonly ShaderBinder _binder;

    public ShaderCompiler() : this(new StandardShaderTypeSystem()) { }

    public ShaderCompiler(ShaderTypeSystem types)
    {
        ArgumentNullException.ThrowIfNull(types);
        _binder = new ShaderBinder(types);
    }

    /// <summary>
    /// Binds, lays out, and emits <paramref name="set"/>, returning a
    /// <see cref="Shader{TVertex}"/> on success. If binding produces any
    /// errors, the result holds <c>null</c> and the diagnostics; emission is
    /// not attempted.
    /// </summary>
    public CompileResult<TVertex> Compile<TVertex>(
        ShaderSet set,
        ShaderVertexLayout ShaderVertexLayout,
        ShaderResourceCounts vertexResources = default,
        ShaderResourceCounts fragmentResources = default)
        where TVertex : unmanaged
    {
        ArgumentNullException.ThrowIfNull(set);
        ArgumentNullException.ThrowIfNull(ShaderVertexLayout);

        var (bound, diagnostics) = _binder.Bind(set);
        if (HasErrors(diagnostics))
            return new CompileResult<TVertex>(null, diagnostics);

        if (bound.Vertex is null || bound.Fragment is null)
        {
            var d = diagnostics.Add(new ShaderDiagnostic(
                ShaderDiagnosticSeverity.Error,
                "SH0300",
                "ShaderSet must contain both a vertex and fragment stage to produce a Shader<TVertex>."));
            return new CompileResult<TVertex>(null, d);
        }

        diagnostics = ValidateShaderVertexLayout(bound.Vertex, ShaderVertexLayout, diagnostics);
        if (HasErrors(diagnostics))
            return new CompileResult<TVertex>(null, diagnostics);

        var laidOut = ShaderLayout.AssignLayout(bound);
        var output = new SpvEmitter().Emit(laidOut);

        var shader = new Shader<TVertex>(
            new StageShader(StageShaderKind.Vertex,   ShaderFormat.Spirv, ImmutableArray.Create(output.Vertex),   vertexResources),
            new StageShader(StageShaderKind.Fragment, ShaderFormat.Spirv, ImmutableArray.Create(output.Fragment), fragmentResources),
            ShaderVertexLayout);

        return new CompileResult<TVertex>(shader, diagnostics);
    }

    private static bool HasErrors(ImmutableList<ShaderDiagnostic> diagnostics)
    {
        foreach (var d in diagnostics)
            if (d.Severity == ShaderDiagnosticSeverity.Error) return true;
        return false;
    }

    /// <summary>
    /// Verifies the vertex stage's <see cref="ShaderGlobalKind.VertexInput"/>
    /// globals line up with <paramref name="ShaderVertexLayout"/>: same count, and
    /// each input's shader-side type matches the corresponding
    /// <see cref="ShaderVertexElementKind"/>. Inputs are matched in declaration
    /// order (which is the order <see cref="ShaderLayout"/> assigns
    /// monotonic locations to them).
    /// </summary>
    private static ImmutableList<ShaderDiagnostic> ValidateShaderVertexLayout(
        ShaderStage vertex,
        ShaderVertexLayout ShaderVertexLayout,
        ImmutableList<ShaderDiagnostic> diagnostics)
    {
        var inputs = vertex.Globals
            .Where(g => g.GlobalKind == ShaderGlobalKind.VertexInput)
            .ToList();

        if (inputs.Count != ShaderVertexLayout.Elements.Length)
        {
            return diagnostics.Add(new ShaderDiagnostic(
                ShaderDiagnosticSeverity.Error,
                "SH0310",
                $"ShaderVertexLayout declares {ShaderVertexLayout.Elements.Length} element(s) but the vertex shader has {inputs.Count} VertexInput(s)."));
        }

        var result = diagnostics;
        for (int i = 0; i < inputs.Count; i++)
        {
            var input = inputs[i];
            var kind = ShaderVertexLayout.Elements[i].Kind;
            if (!IsCompatible(input.Type, kind))
            {
                result = result.Add(new ShaderDiagnostic(
                    ShaderDiagnosticSeverity.Error,
                    "SH0311",
                    $"VertexInput '{input.Name}' (type {Describe(input.Type)}) is not compatible with ShaderVertexLayout element {i} ({kind})."));
            }
        }
        return result;
    }

    private static bool IsCompatible(ShaderType shaderType, ShaderVertexElementKind kind)
        => (kind, shaderType) switch
        {
            (ShaderVertexElementKind.Position3,          VectorType { Component: FloatType, N: 3 }) => true,
            (ShaderVertexElementKind.TextureCoordinate2, VectorType { Component: FloatType, N: 2 }) => true,
            (ShaderVertexElementKind.Color4,             VectorType { Component: FloatType, N: 4 }) => true,
            _ => false,
        };

    private static string Describe(ShaderType type) => type switch
    {
        VectorType v => $"vec{v.N}<{Describe(v.Component)}>",
        FloatType    => "float",
        IntType      => "int",
        UIntType     => "uint",
        BoolType     => "bool",
        _            => type.GetType().Name,
    };

    /// <summary>
    /// Per-stage descriptor sets for uniform buffers in SDL3 GPU's SPIR-V
    /// convention: vertex UBOs in set 1, fragment UBOs in set 3.
    /// </summary>
    private const int VertexUniformSet   = 1;
    private const int FragmentUniformSet = 3;

    /// <summary>
    /// Same as <see cref="Compile{TVertex}"/> but pairs the result with a
    /// typed per-draw arguments value. The shader IR must declare a
    /// <see cref="ShaderGlobalKind.Uniform"/> global in the matching stage
    /// for every element in <paramref name="argsLayout"/>, with binding
    /// <c>(set=1 vertex / set=3 fragment, slot=element.Slot)</c>.
    /// </summary>
    public CompileResult<TVertex, TArgs> Compile<TVertex, TArgs>(
        ShaderSet set,
        ShaderVertexLayout ShaderVertexLayout,
        ShaderArgsLayout argsLayout)
        where TVertex : unmanaged
        where TArgs : unmanaged
    {
        ArgumentNullException.ThrowIfNull(set);
        ArgumentNullException.ThrowIfNull(ShaderVertexLayout);
        ArgumentNullException.ThrowIfNull(argsLayout);

        var (bound, diagnostics) = _binder.Bind(set);
        if (HasErrors(diagnostics))
            return new CompileResult<TVertex, TArgs>(null, diagnostics);

        if (bound.Vertex is null || bound.Fragment is null)
        {
            diagnostics = diagnostics.Add(new ShaderDiagnostic(
                ShaderDiagnosticSeverity.Error,
                "SH0300",
                "ShaderSet must contain both a vertex and fragment stage to produce a Shader<TVertex>."));
            return new CompileResult<TVertex, TArgs>(null, diagnostics);
        }

        diagnostics = ValidateShaderVertexLayout(bound.Vertex, ShaderVertexLayout, diagnostics);
        diagnostics = ValidateArgsLayout(bound, argsLayout, diagnostics);
        if (HasErrors(diagnostics))
            return new CompileResult<TVertex, TArgs>(null, diagnostics);

        var (vertexCount, fragmentCount) = CountUniformsByStage(argsLayout);
        var vertexResources   = new ShaderResourceCounts(NumUniformBuffers: vertexCount);
        var fragmentResources = new ShaderResourceCounts(
            NumSamplers: CountSamplers(bound.Fragment),
            NumUniformBuffers: fragmentCount);

        var laidOut = ShaderLayout.AssignLayout(bound);
        var output = new SpvEmitter().Emit(laidOut);

        var shader = new Shader<TVertex, TArgs>(
            new StageShader(StageShaderKind.Vertex,   ShaderFormat.Spirv, ImmutableArray.Create(output.Vertex),   vertexResources),
            new StageShader(StageShaderKind.Fragment, ShaderFormat.Spirv, ImmutableArray.Create(output.Fragment), fragmentResources),
            ShaderVertexLayout,
            argsLayout);

        return new CompileResult<TVertex, TArgs>(shader, diagnostics);
    }

    private static ImmutableList<ShaderDiagnostic> ValidateArgsLayout(
        ShaderSet bound,
        ShaderArgsLayout argsLayout,
        ImmutableList<ShaderDiagnostic> diagnostics)
    {
        var result = diagnostics;
        foreach (var element in argsLayout.Elements)
        {
            var stage = element.Stage == ShaderArgStage.Vertex ? bound.Vertex : bound.Fragment;
            if (stage is null) continue;

            int expectedSet = element.Stage == ShaderArgStage.Vertex ? VertexUniformSet : FragmentUniformSet;
            ShaderGlobal? match = null;
            foreach (var g in stage.Globals)
            {
                if (g.GlobalKind != ShaderGlobalKind.Uniform) continue;
                if (g.BindingSet != expectedSet) continue;
                if (g.BindingSlot != element.Slot) continue;
                match = g;
                break;
            }

            if (match is null)
            {
                result = result.Add(new ShaderDiagnostic(
                    ShaderDiagnosticSeverity.Error,
                    "SH0320",
                    $"ShaderArgsLayout element ({element.Stage}, slot {element.Slot}, {element.Kind}) has no matching Uniform global with binding (set={expectedSet}, slot={element.Slot}) in the {element.Stage} stage."));
                continue;
            }

            if (!IsUniformTypeCompatible(match.Type, element.Kind))
            {
                result = result.Add(new ShaderDiagnostic(
                    ShaderDiagnosticSeverity.Error,
                    "SH0321",
                    $"Uniform '{match.Name}' (type {Describe(match.Type)}) is not compatible with ShaderArgsLayout element {element.Kind}."));
            }
        }
        return result;
    }

    private static bool IsUniformTypeCompatible(ShaderType shaderType, ShaderArgKind kind)
        => (kind, shaderType) switch
        {
            (ShaderArgKind.Float,     FloatType)                                       => true,
            (ShaderArgKind.Float2,    VectorType { Component: FloatType, N: 2 })       => true,
            (ShaderArgKind.Float3,    VectorType { Component: FloatType, N: 3 })       => true,
            (ShaderArgKind.Float4,    VectorType { Component: FloatType, N: 4 })       => true,
            (ShaderArgKind.Int,       IntType)                                         => true,
            (ShaderArgKind.UInt,      UIntType)                                        => true,
            (ShaderArgKind.Matrix4x4, MatrixType { Component: FloatType, Rows: 4, Cols: 4 }) => true,
            _ => false,
        };

    private static (uint Vertex, uint Fragment) CountUniformsByStage(ShaderArgsLayout layout)
    {
        uint v = 0, f = 0;
        foreach (var e in layout.Elements)
        {
            if (e.Stage == ShaderArgStage.Vertex) v++;
            else f++;
        }
        return (v, f);
    }

    private static uint CountSamplers(ShaderStage stage)
    {
        uint n = 0;
        foreach (var g in stage.Globals)
            if (g.GlobalKind == ShaderGlobalKind.Sampler) n++;
        return n;
    }
}
