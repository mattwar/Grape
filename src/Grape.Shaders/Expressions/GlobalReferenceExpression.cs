namespace Grape.Shaders;

/// <summary>
/// Reference to a module-level <see cref="ShaderGlobal"/>. Carries a
/// <see cref="Name"/> for unbound use and an optional resolved
/// <see cref="Global"/> (set by the binder; sets <see cref="ShaderElement.IsUnbound"/>
/// to false once present).
/// </summary>
public sealed class GlobalReferenceExpression : ShaderExpression
{
    public string Name { get; }
    public ShaderGlobal? Global { get; }

    /// <summary>Unbound construction by name.</summary>
    public GlobalReferenceExpression(string name) : this(name, null, null, null) { }

    /// <summary>Bound construction from an already-resolved global.</summary>
    public GlobalReferenceExpression(ShaderGlobal global)
        : this(global.Name, global, global.Type, null) { }

    private GlobalReferenceExpression(
        string name,
        ShaderGlobal? global,
        ShaderType? resultType,
        ImmutableList<ShaderDiagnostic>? diagnostics)
        : base(NotNullState(global), resultType, diagnostics)
    {
        Name = name;
        Global = global;
    }

    public override GlobalReferenceExpression WithResultType(ShaderType? resultType)
        => ReferenceEquals(resultType, ResultType) ? this
            : new GlobalReferenceExpression(Name, Global, resultType, Diagnostics);

    public override GlobalReferenceExpression WithDiagnostics(ImmutableList<ShaderDiagnostic> diagnostics)
        => diagnostics == Diagnostics ? this
            : new GlobalReferenceExpression(Name, Global, ResultType, diagnostics);

    public GlobalReferenceExpression WithGlobal(ShaderGlobal? global)
        => ReferenceEquals(global, Global) ? this
            : new GlobalReferenceExpression(Name, global, ResultType, Diagnostics);

    public override int ChildCount => 0;
    public override ShaderElement? GetChild(int index) => throw new ArgumentOutOfRangeException(nameof(index));
    public override GlobalReferenceExpression RewriteChildren(ShaderRewriter rewriter) => this;
}
