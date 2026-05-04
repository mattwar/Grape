namespace Grape.Shaders;

/// <summary>Either a built-in intrinsic or a user-defined function.</summary>
public abstract class ShaderCallTarget
{
    private protected ShaderCallTarget() { }
}

public sealed class IntrinsicCallTarget(ShaderIntrinsic op) : ShaderCallTarget
{
    public ShaderIntrinsic Op { get; } = op;
}

public sealed class UserFunctionCallTarget(ShaderFunction function) : ShaderCallTarget
{
    public ShaderFunction Function { get; } = function;
}

/// <summary>Call to a built-in intrinsic or a user-defined <see cref="ShaderFunction"/>.</summary>
public sealed class CallExpression : ShaderExpression
{
    public ShaderCallTarget Target { get; }
    public ImmutableArray<ShaderExpression> Args { get; }

    public CallExpression(ShaderCallTarget target, ImmutableArray<ShaderExpression> args)
        : this(target, args, null, null) { }

    private CallExpression(
        ShaderCallTarget target,
        ImmutableArray<ShaderExpression> args,
        ShaderType? resultType,
        ImmutableList<ShaderDiagnostic>? diagnostics)
        : base(CombineState(args), resultType, diagnostics)
    {
        Target = target;
        Args = args;
    }

    public override CallExpression WithResultType(ShaderType? resultType)
        => ReferenceEquals(resultType, ResultType) ? this
            : new CallExpression(Target, Args, resultType, Diagnostics);

    public override CallExpression WithDiagnostics(ImmutableList<ShaderDiagnostic> diagnostics)
        => diagnostics == Diagnostics ? this
            : new CallExpression(Target, Args, ResultType, diagnostics);

    public CallExpression WithArgs(ImmutableArray<ShaderExpression> args)
        => args == Args ? this : new CallExpression(Target, args, ResultType, Diagnostics);

    public override int ChildCount => Args.Length;
    public override ShaderElement? GetChild(int index) => Args[index];

    public override CallExpression RewriteChildren(ShaderRewriter rewriter)
        => WithArgs(rewriter.Rewrite(Args));
}
