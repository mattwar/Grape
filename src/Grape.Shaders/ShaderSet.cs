namespace Grape.Shaders;

/// <summary>A complete pipeline: typically a vertex stage + fragment stage, or a compute stage.</summary>
public sealed class ShaderSet : ShaderElement
{
    public ShaderStage? Vertex   { get; }
    public ShaderStage? Fragment { get; }
    public ShaderStage? Compute  { get; }

    public ShaderSet(ShaderStage? vertex, ShaderStage? fragment, ShaderStage? compute = null)
        : this(vertex, fragment, compute, null) { }

    private ShaderSet(
        ShaderStage? vertex,
        ShaderStage? fragment,
        ShaderStage? compute,
        ImmutableList<ShaderDiagnostic>? diagnostics)
        : base(State(vertex) | State(fragment) | State(compute), diagnostics)
    {
        Vertex = vertex;
        Fragment = fragment;
        Compute = compute;
    }

    public ShaderSet WithStages(ShaderStage? vertex, ShaderStage? fragment, ShaderStage? compute)
        => ReferenceEquals(vertex, Vertex)
            && ReferenceEquals(fragment, Fragment)
            && ReferenceEquals(compute, Compute)
                ? this
                : new ShaderSet(vertex, fragment, compute, Diagnostics);

    public override ShaderSet WithDiagnostics(ImmutableList<ShaderDiagnostic> diagnostics)
        => diagnostics == Diagnostics ? this
            : new ShaderSet(Vertex, Fragment, Compute, diagnostics);

    public override int ChildCount => 3;
    public override ShaderElement? GetChild(int index) => index switch
    {
        0 => Vertex,
        1 => Fragment,
        2 => Compute,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };

    public override ShaderSet RewriteChildren(ShaderRewriter rewriter)
    {
        var v = (ShaderStage?)rewriter.Rewrite(Vertex);
        var f = (ShaderStage?)rewriter.Rewrite(Fragment);
        var c = (ShaderStage?)rewriter.Rewrite(Compute);
        return WithStages(v, f, c);
    }
}
