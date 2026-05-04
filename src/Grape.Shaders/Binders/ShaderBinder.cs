namespace Grape.Shaders;

/// <summary>
/// Resolves global references and infers result types for every
/// <see cref="ShaderExpression"/> in a tree. Bind errors are recorded as
/// diagnostics on the offending node rather than thrown, so partially-broken
/// trees can still be inspected and re-bound.
/// </summary>
public class ShaderBinder(ShaderTypeSystem types)
{
    /// <summary>Type system used to construct inferred vector/matrix/array result types.</summary>
    public ShaderTypeSystem Types { get; } = types;

    /// <summary>
    /// Binds <paramref name="element"/>: walks the tree bottom-up, resolves
    /// global references against any enclosing <see cref="ShaderStage"/>'s
    /// globals, and infers <see cref="ShaderExpression.ResultType"/> for every
    /// expression. Returns the bound element and a flat list of every
    /// diagnostic recorded anywhere in the tree.
    /// </summary>
    public BindResult<TElement> Bind<TElement>(TElement element) where TElement : ShaderElement
    {
        var bound = (TElement)new Rewriter(this).Rewrite(element)!;
        return new BindResult<TElement>(bound, bound.GetContainedDiagnostics());
    }

    /// <summary>
    /// The rewriter that performs the actual bottom-up binding pass.
    /// Nested in <see cref="ShaderBinder"/> so that binder state (the
    /// global-scope stack) is private to a single binding operation.
    /// </summary>
    private sealed class Rewriter(ShaderBinder binder) : ShaderRewriter
    {
        private ShaderTypeSystem Types => binder.Types;

        private ImmutableDictionary<string, ShaderGlobal> _globals
            = ImmutableDictionary<string, ShaderGlobal>.Empty;

        public override ShaderElement? Rewrite(ShaderElement? node)
        {
            // ShaderStage establishes the global scope visible to its body.
            if (node is ShaderStage stage)
            {
                var saved = _globals;
                try
                {
                    var builder = ImmutableDictionary.CreateBuilder<string, ShaderGlobal>();
                    foreach (var g in stage.Globals)
                        builder[g.Name] = g;
                    _globals = builder.ToImmutable();
                    return base.Rewrite(stage);
                }
                finally
                {
                    _globals = saved;
                }
            }
            return base.Rewrite(node);
        }

        protected override ShaderElement Rewrite(ShaderElement current, ShaderElement original)
        {
            return current switch
            {
                GlobalReferenceExpression g when g.Global is null => BindGlobalReference(g),
                BinaryExpression b      when b.ResultType is null => BindBinary(b),
                UnaryExpression u       when u.ResultType is null => BindUnary(u),
                SwizzleExpression s     when s.ResultType is null => BindSwizzle(s),
                IndexExpression i       when i.ResultType is null => BindIndex(i),
                FieldAccessExpression f when f.ResultType is null => BindField(f),
                CallExpression c        when c.ResultType is null => BindCall(c),
                SampleExpression s      when s.ResultType is null => BindSample(s),
                BlockExpression bl      when bl.ResultType is null => BindBlock(bl),
                AssignExpression a      when a.ResultType is null => BindAssign(a),
                IfExpression iff        when iff.ResultType is null => BindIf(iff),
                _ => current,
            };
        }

        // ---- GlobalReference ---------------------------------------------------

        private ShaderExpression BindGlobalReference(GlobalReferenceExpression node)
        {
            if (_globals.TryGetValue(node.Name, out var g))
                return node.WithGlobal(g).WithResultType(g.Type);
            return Error(node, "SH0001", $"Unresolved global reference '{node.Name}'.");
        }

        // ---- Binary ------------------------------------------------------------

        private ShaderExpression BindBinary(BinaryExpression node)
        {
            var lt = node.Left.ResultType;
            var rt = node.Right.ResultType;
            if (lt is null || rt is null) return node;

            var (result, error) = InferBinary(node.Op, lt, rt);
            return result is not null ? node.WithResultType(result) : Error(node, "SH0010", error!);
        }

        private (ShaderType?, string?) InferBinary(ShaderBinaryOp op, ShaderType lt, ShaderType rt)
            => op switch
            {
                ShaderBinaryOp.And or ShaderBinaryOp.Or
                    => RequireBoolMatching(lt, rt, op),

                ShaderBinaryOp.Eq or ShaderBinaryOp.Ne or ShaderBinaryOp.Lt
                or ShaderBinaryOp.Le or ShaderBinaryOp.Gt or ShaderBinaryOp.Ge
                    => CompareResult(lt, rt, op),

                ShaderBinaryOp.BitAnd or ShaderBinaryOp.BitOr or ShaderBinaryOp.BitXor
                or ShaderBinaryOp.Shl or ShaderBinaryOp.Shr
                    => RequireIntegralMatching(lt, rt, op),

                ShaderBinaryOp.MatMul => MatMulResult(lt, rt),

                // Add / Sub / Mul / Div / Rem -- componentwise with scalar broadcast.
                _ => ArithmeticResult(lt, rt, op),
            };

        private static (ShaderType?, string?) ArithmeticResult(ShaderType lt, ShaderType rt, ShaderBinaryOp op)
        {
            if (IsNumericScalar(lt) && IsNumericScalar(rt))
                return ReferenceEquals(lt, rt) ? (lt, null) : (null, $"{op} requires matching scalar types.");

            if (lt is VectorType lv && rt is VectorType rv)
                return ReferenceEquals(lv, rv) ? (lv, null) : (null, $"{op} requires matching vector types.");

            if (lt is VectorType v1 && IsNumericScalar(rt) && ReferenceEquals(v1.Component, rt)) return (v1, null);
            if (rt is VectorType v2 && IsNumericScalar(lt) && ReferenceEquals(v2.Component, lt)) return (v2, null);

            if (lt is MatrixType lm && rt is MatrixType rm && ReferenceEquals(lm, rm)) return (lm, null);
            if (lt is MatrixType m1 && IsNumericScalar(rt) && ReferenceEquals(m1.Component, rt)) return (m1, null);
            if (rt is MatrixType m2 && IsNumericScalar(lt) && ReferenceEquals(m2.Component, lt)) return (m2, null);

            return (null, $"{op} not defined for the given operand types.");
        }

        private (ShaderType?, string?) CompareResult(ShaderType lt, ShaderType rt, ShaderBinaryOp op)
        {
            bool ordering = op is ShaderBinaryOp.Lt or ShaderBinaryOp.Le or ShaderBinaryOp.Gt or ShaderBinaryOp.Ge;

            if (lt is VectorType lv && rt is VectorType rv && ReferenceEquals(lv, rv))
            {
                if (ordering && lv.Component is BoolType) return (null, "Ordering comparison not defined for bool.");
                return (Types.GetVector(ShaderTypeSystem.Bool, lv.N), null);
            }
            if (IsScalar(lt) && IsScalar(rt) && ReferenceEquals(lt, rt))
            {
                if (ordering && lt is BoolType) return (null, "Ordering comparison not defined for bool.");
                return (ShaderTypeSystem.Bool, null);
            }
            return (null, $"{op} requires matching scalar or vector operands.");
        }

        private static (ShaderType?, string?) RequireBoolMatching(ShaderType lt, ShaderType rt, ShaderBinaryOp op)
        {
            if (!ReferenceEquals(lt, rt)) return (null, $"{op} requires matching operand types.");
            if (lt is BoolType) return (lt, null);
            if (lt is VectorType v && v.Component is BoolType) return (lt, null);
            return (null, $"{op} requires bool scalar or vector.");
        }

        private static (ShaderType?, string?) RequireIntegralMatching(ShaderType lt, ShaderType rt, ShaderBinaryOp op)
        {
            if (!ReferenceEquals(lt, rt)) return (null, $"{op} requires matching operand types.");
            if (IsIntegralScalarOrVector(lt)) return (lt, null);
            return (null, $"{op} requires integral scalar or vector.");
        }

        private (ShaderType?, string?) MatMulResult(ShaderType lt, ShaderType rt)
        {
            if (lt is MatrixType mL && rt is VectorType vR
                && ReferenceEquals(mL.Component, vR.Component) && mL.Cols == vR.N)
                return (Types.GetVector(mL.Component, mL.Rows), null);

            if (lt is VectorType vL && rt is MatrixType mR
                && ReferenceEquals(vL.Component, mR.Component) && mR.Rows == vL.N)
                return (Types.GetVector(mR.Component, mR.Cols), null);

            if (lt is MatrixType m1 && rt is MatrixType m2
                && ReferenceEquals(m1.Component, m2.Component) && m1.Cols == m2.Rows)
                return (Types.GetMatrix(m1.Component, m1.Rows, m2.Cols), null);

            return (null, "MatMul operands have incompatible shapes.");
        }

        // ---- Unary -------------------------------------------------------------

        private ShaderExpression BindUnary(UnaryExpression node)
        {
            var t = node.Operand.ResultType;
            if (t is null) return node;

            switch (node.Op)
            {
                case ShaderUnaryOp.Neg:
                    if (!IsNumericScalarOrVector(t)) return Error(node, "SH0011", "Unary negate requires numeric scalar or vector.");
                    break;
                case ShaderUnaryOp.Not:
                    if (!IsBoolScalarOrVector(t)) return Error(node, "SH0012", "Logical not requires bool scalar or vector.");
                    break;
                case ShaderUnaryOp.BitNot:
                    if (!IsIntegralScalarOrVector(t)) return Error(node, "SH0013", "Bitwise not requires integral scalar or vector.");
                    break;
            }
            return node.WithResultType(t);
        }

        // ---- Swizzle / Index / Field ------------------------------------------

        private ShaderExpression BindSwizzle(SwizzleExpression node)
        {
            var st = node.Source.ResultType;
            if (st is null) return node;
            if (st is not VectorType v) return Error(node, "SH0020", "Swizzle source must be a vector.");

            var c = node.Components;
            if (c.Length is < 1 or > 4) return Error(node, "SH0021", "Swizzle must have 1 to 4 components.");
            foreach (var ch in c)
            {
                int idx = SwizzleIndex(ch);
                if (idx < 0)        return Error(node, "SH0022", $"Invalid swizzle component '{ch}'.");
                if (idx >= v.N)     return Error(node, "SH0023", $"Swizzle component '{ch}' out of range for vector of length {v.N}.");
            }
            var result = c.Length == 1 ? v.Component : Types.GetVector(v.Component, c.Length);
            return node.WithResultType(result);
        }

        private ShaderExpression BindIndex(IndexExpression node)
        {
            var st = node.Source.ResultType;
            var it = node.Index.ResultType;
            if (st is null || it is null) return node;
            if (it is not (IntType or UIntType)) return Error(node, "SH0030", "Index must be int or uint.");

            ShaderType? element = st switch
            {
                VectorType v => v.Component,
                MatrixType m => Types.GetVector(m.Component, m.Rows),
                ArrayType a  => a.Element,
                _ => null,
            };
            return element is null
                ? Error(node, "SH0031", "Index source must be vector, matrix, or array.")
                : node.WithResultType(element);
        }

        private ShaderExpression BindField(FieldAccessExpression node)
        {
            var st = node.Source.ResultType;
            if (st is null) return node;
            if (st is not StructType s) return Error(node, "SH0040", "Field access source must be a struct.");
            foreach (var f in s.Fields)
                if (f.Name == node.FieldName)
                    return node.WithResultType(f.Type);
            return Error(node, "SH0041", $"Struct '{s.Name}' has no field '{node.FieldName}'.");
        }

        // ---- Call / Sample -----------------------------------------------------

        private ShaderExpression BindCall(CallExpression node)
        {
            for (int i = 0; i < node.Args.Length; i++)
                if (node.Args[i].ResultType is null) return node;

            switch (node.Target)
            {
                case UserFunctionCallTarget user:
                    {
                        var f = user.Function;
                        if (node.Args.Length != f.Parameters.Length)
                            return Error(node, "SH0050", $"Function '{f.Name}' expects {f.Parameters.Length} args, got {node.Args.Length}.");
                        for (int i = 0; i < node.Args.Length; i++)
                        {
                            var pt = f.Parameters[i].ResultType;
                            if (pt is null) return node; // can't validate yet
                            if (!ReferenceEquals(node.Args[i].ResultType, pt))
                                return Error(node, "SH0051", $"Argument {i} to '{f.Name}' has wrong type.");
                        }
                        return node.WithResultType(f.ReturnType);
                    }

                case IntrinsicCallTarget intr:
                    {
                        var argTypes = new ShaderType[node.Args.Length];
                        for (int i = 0; i < argTypes.Length; i++) argTypes[i] = node.Args[i].ResultType!;
                        var (result, error) = InferIntrinsic(intr.Op, argTypes);
                        return result is not null ? node.WithResultType(result) : Error(node, "SH0052", error!);
                    }

                default:
                    return Error(node, "SH0053", "Unknown call target.");
            }
        }

        private (ShaderType?, string?) InferIntrinsic(ShaderIntrinsic op, ShaderType[] args)
        {
            switch (op)
            {
                case ShaderIntrinsic.Dot:
                    if (args.Length != 2) return Arity(op, 2);
                    if (args[0] is VectorType v && ReferenceEquals(args[0], args[1])) return (v.Component, null);
                    return (null, "dot requires two matching vectors.");

                case ShaderIntrinsic.Cross:
                    if (args.Length != 2) return Arity(op, 2);
                    if (args[0] is VectorType cv && cv.N == 3 && ReferenceEquals(args[0], args[1])) return (cv, null);
                    return (null, "cross requires two vec3 arguments.");

                case ShaderIntrinsic.Length:
                    if (args.Length != 1) return Arity(op, 1);
                    if (args[0] is VectorType lv) return (lv.Component, null);
                    if (IsNumericScalar(args[0])) return (args[0], null);
                    return (null, "length requires scalar or vector.");

                case ShaderIntrinsic.Distance:
                    if (args.Length != 2) return Arity(op, 2);
                    if (!ReferenceEquals(args[0], args[1])) return (null, "distance requires matching arguments.");
                    if (args[0] is VectorType dv) return (dv.Component, null);
                    if (IsNumericScalar(args[0])) return (args[0], null);
                    return (null, "distance requires scalar or vector.");

                case ShaderIntrinsic.Normalize:
                case ShaderIntrinsic.Reflect:
                case ShaderIntrinsic.Refract:
                    if (args[0] is not VectorType) return (null, $"{op} requires a vector first argument.");
                    return (args[0], null);

                case ShaderIntrinsic.Transpose:
                    if (args.Length != 1 || args[0] is not MatrixType m) return (null, "transpose requires a matrix.");
                    return (Types.GetMatrix(m.Component, m.Cols, m.Rows), null);

                case ShaderIntrinsic.Determinant:
                    if (args.Length != 1 || args[0] is not MatrixType md || md.Rows != md.Cols)
                        return (null, "determinant requires a square matrix.");
                    return (md.Component, null);

                case ShaderIntrinsic.Inverse:
                    if (args.Length != 1 || args[0] is not MatrixType mi || mi.Rows != mi.Cols)
                        return (null, "inverse requires a square matrix.");
                    return (mi, null);

                case ShaderIntrinsic.AsFloat:
                    if (args.Length != 1) return Arity(op, 1);
                    return (CoerceComponent(args[0], ShaderTypeSystem.Float), null);
                case ShaderIntrinsic.AsInt:
                    if (args.Length != 1) return Arity(op, 1);
                    return (CoerceComponent(args[0], ShaderTypeSystem.Int), null);
                case ShaderIntrinsic.AsUInt:
                    if (args.Length != 1) return Arity(op, 1);
                    return (CoerceComponent(args[0], ShaderTypeSystem.UInt), null);

                case ShaderIntrinsic.Mix:
                case ShaderIntrinsic.SmoothStep:
                case ShaderIntrinsic.Clamp:
                    if (args.Length != 3) return Arity(op, 3);
                    if (!ReferenceEquals(args[0], args[1]) || !ReferenceEquals(args[1], args[2]))
                        return (null, $"{op} requires three matching arguments.");
                    return (args[0], null);

                case ShaderIntrinsic.Step:
                    if (args.Length != 2) return Arity(op, 2);
                    if (!ReferenceEquals(args[0], args[1])) return (null, "step requires two matching arguments.");
                    return (args[0], null);

                case ShaderIntrinsic.Min:
                case ShaderIntrinsic.Max:
                case ShaderIntrinsic.Pow:
                case ShaderIntrinsic.Mod:
                case ShaderIntrinsic.Atan2:
                    if (args.Length != 2) return Arity(op, 2);
                    if (!ReferenceEquals(args[0], args[1])) return (null, $"{op} requires matching arguments.");
                    if (!IsNumericScalarOrVector(args[0])) return (null, $"{op} requires numeric scalar or vector.");
                    return (args[0], null);

                case ShaderIntrinsic.Saturate:
                    if (args.Length != 1) return Arity(op, 1);
                    if (!IsNumericScalarOrVector(args[0])) return (null, "saturate requires numeric scalar or vector.");
                    return (args[0], null);

                case ShaderIntrinsic.Ddx:
                case ShaderIntrinsic.Ddy:
                case ShaderIntrinsic.FWidth:
                    if (args.Length != 1) return Arity(op, 1);
                    return (args[0], null);

                // Default: unary componentwise math (Abs, Sign, Floor, ..., Sin, Cos, Exp, Log, Sqrt, ...).
                default:
                    if (args.Length != 1) return Arity(op, 1);
                    if (!IsNumericScalarOrVector(args[0])) return (null, $"{op} requires numeric scalar or vector.");
                    return (args[0], null);
            }
        }

        private ShaderExpression BindSample(SampleExpression node)
        {
            var tt = node.Texture.ResultType;
            var st = node.Sampler.ResultType;
            var ct = node.Coord.ResultType;
            if (tt is null || st is null || ct is null) return node;
            if (node.Lod is not null && node.Lod.ResultType is null) return node;

            if (tt is not (Texture2DType or Texture3DType or TextureCubeType or Texture2DArrayType))
                return Error(node, "SH0060", "Sample texture argument must be a texture type.");
            if (st is not SamplerType)
                return Error(node, "SH0061", "Sample sampler argument must be a sampler.");

            return node.WithResultType(Types.GetVector(ShaderTypeSystem.Float, 4));
        }

        // ---- Block / Assign / If ----------------------------------------------

        private static ShaderExpression BindBlock(BlockExpression node)
        {
            if (node.Body.Length == 0) return node.WithResultType(ShaderTypeSystem.Void);
            var last = node.Body[^1].ResultType;
            if (last is null) return node;
            return node.WithResultType(last);
        }

        private static ShaderExpression BindAssign(AssignExpression node)
        {
            var tt = node.Target.ResultType;
            var vt = node.Value.ResultType;
            if (tt is null || vt is null) return node;

            if (node.Target is not (ParameterExpression or GlobalReferenceExpression
                                  or SwizzleExpression or IndexExpression or FieldAccessExpression))
                return Error(node, "SH0070", "Assign target must be a parameter, global, swizzle, index, or field access.");
            if (tt is VoidType)
                return Error(node, "SH0071", "Assign target cannot have void type.");
            if (!ReferenceEquals(tt, vt))
                return Error(node, "SH0072", "Assign value type does not match target type.");

            return node.WithResultType(vt);
        }

        private static ShaderExpression BindIf(IfExpression node)
        {
            var testType = node.Test.ResultType;
            if (testType is null) return node;
            if (testType is not BoolType) return Error(node, "SH0080", "If test must be bool.");

            if (node.IfFalse is null)
                return node.WithResultType(ShaderTypeSystem.Void);

            var at = node.IfTrue.ResultType;
            var bt = node.IfFalse.ResultType;
            if (at is null || bt is null) return node;

            var common = (at is not VoidType && ReferenceEquals(at, bt)) ? at : ShaderTypeSystem.Void;
            return node.WithResultType(common);
        }

        // ---- Helpers -----------------------------------------------------------

        private static T Error<T>(T node, string code, string message) where T : ShaderElement
            => (T)node.WithDiagnostics(node.Diagnostics.Add(
                new ShaderDiagnostic(ShaderDiagnosticSeverity.Error, code, message)));

        private static (ShaderType?, string?) Arity(ShaderIntrinsic op, int expected)
            => (null, $"{op} expects {expected} argument(s).");

        private ShaderType CoerceComponent(ShaderType t, ShaderType newComponent)
        {
            if (IsScalar(t)) return newComponent;
            if (t is VectorType v) return Types.GetVector(newComponent, v.N);
            return t;
        }

        private static bool IsScalar(ShaderType t) => t is BoolType or IntType or UIntType or FloatType;
        private static bool IsNumericScalar(ShaderType t) => t is IntType or UIntType or FloatType;
        private static bool IsBoolScalarOrVector(ShaderType t)
            => t is BoolType || (t is VectorType v && v.Component is BoolType);
        private static bool IsNumericScalarOrVector(ShaderType t)
            => IsNumericScalar(t) || (t is VectorType v && IsNumericScalar(v.Component));
        private static bool IsIntegralScalarOrVector(ShaderType t)
            => t is IntType or UIntType || (t is VectorType v && v.Component is IntType or UIntType);

        private static int SwizzleIndex(char c) => c switch
        {
            'x' or 'r' => 0,
            'y' or 'g' => 1,
            'z' or 'b' => 2,
            'w' or 'a' => 3,
            _ => -1,
        };
    }
}

/// <summary>
/// Result of <see cref="ShaderBinder.Bind{TElement}(TElement)"/>: the bound
/// element and a flat list of every diagnostic recorded in the tree.
/// </summary>
public sealed class BindResult<TElement>(TElement element, ImmutableList<ShaderDiagnostic> diagnostics)
    where TElement : ShaderElement
{
    public TElement Element { get; } = element;
    public ImmutableList<ShaderDiagnostic> Diagnostics { get; } = diagnostics;

    /// <summary>True if the element has any unresolved binding facts in the tree.</summary>
    public bool IsUnbound => Element.IsUnbound;

    /// <summary>True if any diagnostic in <see cref="Diagnostics"/> has Error severity.</summary>
    public bool HasErrors
    {
        get
        {
            foreach (var d in Diagnostics)
                if (d.Severity == ShaderDiagnosticSeverity.Error) return true;
            return false;
        }
    }

    public void Deconstruct(out TElement element, out ImmutableList<ShaderDiagnostic> diagnostics)
    {
        element = Element;
        diagnostics = Diagnostics;
    }
}
