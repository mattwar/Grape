namespace Grape.Shaders;

/// <summary>Declare a local. Always void-typed; the variable carries its own type.</summary>
public sealed class DeclareLocalExpression : ShaderExpression
{
    public ParameterExpression Variable { get; }
    public ShaderExpression? Initializer { get; }
    public bool IsMutable { get; }

    public DeclareLocalExpression(ParameterExpression variable, ShaderExpression? initializer, bool isMutable)
        : this(variable, initializer, isMutable, null) { }

    private DeclareLocalExpression(
        ParameterExpression variable,
        ShaderExpression? initializer,
        bool isMutable,
        ImmutableList<ShaderDiagnostic>? diagnostics)
        : base(State(variable) | State(initializer), ShaderTypeSystem.Void, diagnostics)
    {
        Variable = variable;
        Initializer = initializer;
        IsMutable = isMutable;
    }

    public override DeclareLocalExpression WithResultType(ShaderType? resultType) => this;

    public override DeclareLocalExpression WithDiagnostics(ImmutableList<ShaderDiagnostic> diagnostics)
        => diagnostics == Diagnostics ? this
            : new DeclareLocalExpression(Variable, Initializer, IsMutable, diagnostics);

    public DeclareLocalExpression WithChildren(ParameterExpression variable, ShaderExpression? initializer)
        => ReferenceEquals(variable, Variable) && ReferenceEquals(initializer, Initializer) ? this
            : new DeclareLocalExpression(variable, initializer, IsMutable, Diagnostics);

    public override int ChildCount => 2;
    public override ShaderElement? GetChild(int index) => index switch
    {
        0 => Variable,
        1 => Initializer,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };

    public override DeclareLocalExpression RewriteChildren(ShaderRewriter rewriter)
    {
        var v = (ParameterExpression)rewriter.Rewrite(Variable)!;
        var i = (ShaderExpression?)rewriter.Rewrite(Initializer);
        return WithChildren(v, i);
    }
}
