namespace Grape.Shaders;

/// <summary>
/// Swizzle on a vector. <see cref="Components"/> is 1-4 chars from {x,y,z,w}
/// or {r,g,b,a}. Single-char produces a scalar; multi-char produces a vector.
/// </summary>
public sealed class SwizzleExpression : ShaderExpression
{
    public ShaderExpression Source { get; }
    public string Components { get; }

    public SwizzleExpression(ShaderExpression source, string components)
        : this(source, components, null, null) { }

    private SwizzleExpression(
        ShaderExpression source,
        string components,
        ShaderType? resultType,
        ImmutableList<ShaderDiagnostic>? diagnostics)
        : base(State(source), resultType, diagnostics)
    {
        Source = source;
        Components = components;
    }

    public override SwizzleExpression WithResultType(ShaderType? resultType)
        => ReferenceEquals(resultType, ResultType) ? this
            : new SwizzleExpression(Source, Components, resultType, Diagnostics);

    public override SwizzleExpression WithDiagnostics(ImmutableList<ShaderDiagnostic> diagnostics)
        => diagnostics == Diagnostics ? this
            : new SwizzleExpression(Source, Components, ResultType, diagnostics);

    public SwizzleExpression WithSource(ShaderExpression source)
        => ReferenceEquals(source, Source) ? this
            : new SwizzleExpression(source, Components, ResultType, Diagnostics);

    public override int ChildCount => 1;
    public override ShaderElement? GetChild(int index) => index == 0
        ? Source
        : throw new ArgumentOutOfRangeException(nameof(index));

    public override SwizzleExpression RewriteChildren(ShaderRewriter rewriter)
        => WithSource((ShaderExpression)rewriter.Rewrite(Source)!);
}
