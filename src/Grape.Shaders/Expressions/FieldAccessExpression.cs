namespace Grape.Shaders;

/// <summary>Static field access on a struct value.</summary>
public sealed class FieldAccessExpression : ShaderExpression
{
    public ShaderExpression Source { get; }
    public string FieldName { get; }

    public FieldAccessExpression(ShaderExpression source, string fieldName)
        : this(source, fieldName, null, null) { }

    private FieldAccessExpression(
        ShaderExpression source,
        string fieldName,
        ShaderType? resultType,
        ImmutableList<ShaderDiagnostic>? diagnostics)
        : base(State(source), resultType, diagnostics)
    {
        Source = source;
        FieldName = fieldName;
    }

    public override FieldAccessExpression WithResultType(ShaderType? resultType)
        => ReferenceEquals(resultType, ResultType) ? this
            : new FieldAccessExpression(Source, FieldName, resultType, Diagnostics);

    public override FieldAccessExpression WithDiagnostics(ImmutableList<ShaderDiagnostic> diagnostics)
        => diagnostics == Diagnostics ? this
            : new FieldAccessExpression(Source, FieldName, ResultType, diagnostics);

    public FieldAccessExpression WithSource(ShaderExpression source)
        => ReferenceEquals(source, Source) ? this
            : new FieldAccessExpression(source, FieldName, ResultType, Diagnostics);

    public override int ChildCount => 1;
    public override ShaderElement? GetChild(int index) => index == 0
        ? Source
        : throw new ArgumentOutOfRangeException(nameof(index));

    public override FieldAccessExpression RewriteChildren(ShaderRewriter rewriter)
        => WithSource((ShaderExpression)rewriter.Rewrite(Source)!);
}
