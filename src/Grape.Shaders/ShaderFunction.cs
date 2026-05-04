namespace Grape.Shaders;

/// <summary>A user-defined helper function. Pure: value in, value out.</summary>
public sealed class ShaderFunction : ShaderElement
{
    public string Name { get; }
    public ShaderType ReturnType { get; }
    public ImmutableArray<ParameterExpression> Parameters { get; }
    public ShaderExpression Body { get; }

    public ShaderFunction(
        string name,
        ShaderType returnType,
        ImmutableArray<ParameterExpression> parameters,
        ShaderExpression body)
        : this(name, returnType, parameters, body, null) { }

    private ShaderFunction(
        string name,
        ShaderType returnType,
        ImmutableArray<ParameterExpression> parameters,
        ShaderExpression body,
        ImmutableList<ShaderDiagnostic>? diagnostics)
        : base(CombineState(parameters) | State(body), diagnostics)
    {
        Name = name;
        ReturnType = returnType;
        Parameters = parameters;
        Body = body;
    }

    public ShaderFunction WithChildren(ImmutableArray<ParameterExpression> parameters, ShaderExpression body)
        => parameters == Parameters && ReferenceEquals(body, Body) ? this
            : new ShaderFunction(Name, ReturnType, parameters, body, Diagnostics);

    public override ShaderFunction WithDiagnostics(ImmutableList<ShaderDiagnostic> diagnostics)
        => diagnostics == Diagnostics ? this
            : new ShaderFunction(Name, ReturnType, Parameters, Body, diagnostics);

    public override int ChildCount => Parameters.Length + 1;
    public override ShaderElement? GetChild(int index)
    {
        if (index < Parameters.Length) return Parameters[index];
        if (index == Parameters.Length) return Body;
        throw new ArgumentOutOfRangeException(nameof(index));
    }

    public override ShaderFunction RewriteChildren(ShaderRewriter rewriter)
    {
        var ps = rewriter.Rewrite(Parameters);
        var b = (ShaderExpression)rewriter.Rewrite(Body)!;
        return WithChildren(ps, b);
    }
}
