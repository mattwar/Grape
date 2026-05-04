namespace Grape.Shaders;

public enum ShaderStageKind { Vertex, Fragment, Compute }

/// <summary>One stage of a shader pipeline.</summary>
public sealed class ShaderStage : ShaderElement
{
    public ShaderStageKind Stage { get; }
    public ImmutableArray<ShaderGlobal> Globals { get; }
    public ImmutableArray<ShaderFunction> Functions { get; }
    public ShaderExpression EntryBody { get; }

    public ShaderStage(
        ShaderStageKind stage,
        ImmutableArray<ShaderGlobal> globals,
        ImmutableArray<ShaderFunction> functions,
        ShaderExpression entryBody)
        : this(stage, globals, functions, entryBody, null) { }

    private ShaderStage(
        ShaderStageKind stage,
        ImmutableArray<ShaderGlobal> globals,
        ImmutableArray<ShaderFunction> functions,
        ShaderExpression entryBody,
        ImmutableList<ShaderDiagnostic>? diagnostics)
        : base(CombineState(globals) | CombineState(functions) | State(entryBody), diagnostics)
    {
        Stage = stage;
        Globals = globals;
        Functions = functions;
        EntryBody = entryBody;
    }

    public ShaderStage WithChildren(
        ImmutableArray<ShaderGlobal> globals,
        ImmutableArray<ShaderFunction> functions,
        ShaderExpression entryBody)
        => globals == Globals && functions == Functions && ReferenceEquals(entryBody, EntryBody) ? this
            : new ShaderStage(Stage, globals, functions, entryBody, Diagnostics);

    public override ShaderStage WithDiagnostics(ImmutableList<ShaderDiagnostic> diagnostics)
        => diagnostics == Diagnostics ? this
            : new ShaderStage(Stage, Globals, Functions, EntryBody, diagnostics);

    public override int ChildCount => Globals.Length + Functions.Length + 1;
    public override ShaderElement? GetChild(int index)
    {
        if (index < Globals.Length) return Globals[index];
        index -= Globals.Length;
        if (index < Functions.Length) return Functions[index];
        index -= Functions.Length;
        if (index == 0) return EntryBody;
        throw new ArgumentOutOfRangeException(nameof(index));
    }

    public override ShaderStage RewriteChildren(ShaderRewriter rewriter)
    {
        var g = rewriter.Rewrite(Globals);
        var f = rewriter.Rewrite(Functions);
        var e = (ShaderExpression)rewriter.Rewrite(EntryBody)!;
        return WithChildren(g, f, e);
    }
}
