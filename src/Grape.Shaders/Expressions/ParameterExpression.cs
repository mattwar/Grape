namespace Grape.Shaders;

/// <summary>
/// A named, typed binding: function parameter, let-bound local, or loop variable.
/// Identity is by reference; <see cref="Name"/> is for diagnostics and emit.
/// </summary>
public sealed class ParameterExpression : ShaderExpression
{
    public string Name { get; }

    public ParameterExpression(string name, ShaderType type) : this(name, type, null) { }

    private ParameterExpression(string name, ShaderType? type, ImmutableList<ShaderDiagnostic>? diagnostics)
        : base(ContainsState.None, type, diagnostics)
    {
        Name = name;
    }

    public override ParameterExpression WithResultType(ShaderType? resultType)
        => ReferenceEquals(resultType, ResultType) ? this : new ParameterExpression(Name, resultType, Diagnostics);

    public override ParameterExpression WithDiagnostics(ImmutableList<ShaderDiagnostic> diagnostics)
        => diagnostics == Diagnostics ? this : new ParameterExpression(Name, ResultType, diagnostics);

    public override int ChildCount => 0;
    public override ShaderElement? GetChild(int index) => throw new ArgumentOutOfRangeException(nameof(index));
    public override ParameterExpression RewriteChildren(ShaderRewriter rewriter) => this;
}
