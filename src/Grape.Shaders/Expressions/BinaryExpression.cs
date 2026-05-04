namespace Grape.Shaders;

public sealed class BinaryExpression : ShaderExpression
{
    public ShaderBinaryOp Op { get; }
    public ShaderExpression Left  { get; }
    public ShaderExpression Right { get; }

    public BinaryExpression(ShaderBinaryOp op, ShaderExpression left, ShaderExpression right)
        : this(op, left, right, null, null) { }

    private BinaryExpression(
        ShaderBinaryOp op,
        ShaderExpression left,
        ShaderExpression right,
        ShaderType? resultType,
        ImmutableList<ShaderDiagnostic>? diagnostics)
        : base(State(left) | State(right), resultType, diagnostics)
    {
        Op = op;
        Left = left;
        Right = right;
    }

    public override BinaryExpression WithResultType(ShaderType? resultType)
        => ReferenceEquals(resultType, ResultType) ? this
            : new BinaryExpression(Op, Left, Right, resultType, Diagnostics);

    public override BinaryExpression WithDiagnostics(ImmutableList<ShaderDiagnostic> diagnostics)
        => diagnostics == Diagnostics ? this
            : new BinaryExpression(Op, Left, Right, ResultType, diagnostics);

    public BinaryExpression WithOperands(ShaderExpression left, ShaderExpression right)
        => ReferenceEquals(left, Left) && ReferenceEquals(right, Right) ? this
            : new BinaryExpression(Op, left, right, ResultType, Diagnostics);

    public override int ChildCount => 2;
    public override ShaderElement? GetChild(int index) => index switch
    {
        0 => Left,
        1 => Right,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };

    public override BinaryExpression RewriteChildren(ShaderRewriter rewriter)
    {
        var l = (ShaderExpression)rewriter.Rewrite(Left)!;
        var r = (ShaderExpression)rewriter.Rewrite(Right)!;
        return WithOperands(l, r);
    }
}
