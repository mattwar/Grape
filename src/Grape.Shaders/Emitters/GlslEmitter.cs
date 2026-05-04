using System.Globalization;
using System.Linq;
using System.Text;

namespace Grape.Shaders.Emitters;

/// <summary>Output of <see cref="GlslEmitter.Emit(ShaderSet)"/>: per-stage GLSL source.</summary>
public sealed class GlslEmitOutput
{
    public string? Vertex   { get; init; }
    public string? Fragment { get; init; }
    public string? Compute  { get; init; }
}

/// <summary>
/// Emits GLSL 4.50 (Vulkan profile) text from a bound <see cref="ShaderSet"/>.
/// The set must be fully bound (no <see cref="ShaderElement.IsUnbound"/>);
/// otherwise an <see cref="InvalidOperationException"/> is thrown.
/// </summary>
public sealed class GlslEmitter
{
    public GlslEmitOutput Emit(ShaderSet set)
    {
        if (set.IsUnbound)
            throw new InvalidOperationException("ShaderSet is not fully bound; run ShaderBinder before emitting.");
        return new GlslEmitOutput
        {
            Vertex   = set.Vertex   is null ? null : Emit(set.Vertex),
            Fragment = set.Fragment is null ? null : Emit(set.Fragment),
            Compute  = set.Compute  is null ? null : Emit(set.Compute),
        };
    }

    public string Emit(ShaderStage stage)
    {
        if (stage.IsUnbound)
            throw new InvalidOperationException("ShaderStage is not fully bound; run ShaderBinder before emitting.");

        var w = new Writer();
        w.WriteLine("#version 450");
        w.WriteLine();

        var hasDecls = false;
        foreach (var g in stage.Globals)
            hasDecls |= EmitGlobal(w, g);
        if (hasDecls) w.WriteLine();

        foreach (var f in stage.Functions)
        {
            EmitFunction(w, f);
            w.WriteLine();
        }

        w.Write("void main() ");
        EmitBlock(w, stage.EntryBody);
        w.WriteLine();
        return w.ToString();
    }

    // -----------------------------------------------------------------------
    // Globals
    // -----------------------------------------------------------------------

    private static bool EmitGlobal(Writer w, ShaderGlobal g)
    {
        // Built-ins (gl_Position, gl_FragCoord, ...) are implicit -- no declaration.
        if (g.Builtin != ShaderBuiltin.None || g.GlobalKind == ShaderGlobalKind.Builtin)
            return false;

        switch (g.GlobalKind)
        {
            case ShaderGlobalKind.VertexInput:
            case ShaderGlobalKind.StageInput:
                w.WriteLine($"{LayoutLocation(g)}in {TypeDecl(g.Type, g.Name)};");
                return true;

            case ShaderGlobalKind.StageOutput:
                w.WriteLine($"{LayoutLocation(g)}out {TypeDecl(g.Type, g.Name)};");
                return true;

            case ShaderGlobalKind.Uniform:
                if (IsOpaque(g.Type))
                {
                    w.WriteLine($"{LayoutBinding(g)}uniform {TypeName(g.Type)} {g.Name};");
                }
                else
                {
                    // Unnamed UBO: the field name is exposed directly into the shader scope.
                    w.WriteLine($"{LayoutBinding(g)}uniform _ub_{g.Name} {{ {TypeDecl(g.Type, g.Name)}; }};");
                }
                return true;

            case ShaderGlobalKind.PushConstant:
                w.WriteLine($"layout(push_constant) uniform _pc_{g.Name} {{ {TypeDecl(g.Type, g.Name)}; }};");
                return true;

            case ShaderGlobalKind.Texture:
                w.WriteLine($"{LayoutBinding(g)}uniform {TextureTypeName(g.Type)} {g.Name};");
                return true;

            case ShaderGlobalKind.Sampler:
                w.WriteLine($"{LayoutBinding(g)}uniform sampler {g.Name};");
                return true;

            default:
                throw new InvalidOperationException($"Unknown global kind {g.GlobalKind}.");
        }
    }

    private static bool IsOpaque(ShaderType t) => t is SamplerType
        or Texture2DType or Texture3DType or TextureCubeType or Texture2DArrayType;

    private static string LayoutLocation(ShaderGlobal g)
        => g.Location is int loc ? $"layout(location = {loc}) " : "";

    private static string LayoutBinding(ShaderGlobal g)
    {
        if (g.BindingSet is int s && g.BindingSlot is int b)
            return $"layout(set = {s}, binding = {b}) ";
        if (g.BindingSlot is int b2)
            return $"layout(binding = {b2}) ";
        return "";
    }

    // -----------------------------------------------------------------------
    // Functions
    // -----------------------------------------------------------------------

    private void EmitFunction(Writer w, ShaderFunction f)
    {
        w.Write($"{TypeName(f.ReturnType)} {f.Name}(");
        for (int i = 0; i < f.Parameters.Length; i++)
        {
            if (i > 0) w.Write(", ");
            var p = f.Parameters[i];
            w.Write($"{TypeName(RequireType(p))} {p.Name}");
        }
        w.Write(") ");

        // Function body. If it's a block, emit children as statements.
        // Otherwise wrap in an implicit return (or emit as a statement for void).
        w.WriteLine("{");
        w.Indent();
        if (f.Body is BlockExpression block)
        {
            foreach (var s in block.Body) EmitStatement(w, s);
        }
        else if (f.ReturnType is VoidType)
        {
            EmitStatement(w, f.Body);
        }
        else
        {
            w.Write("return ");
            EmitExpression(w, f.Body);
            w.WriteLine(";");
        }
        w.Dedent();
        w.WriteLine("}");
    }

    // -----------------------------------------------------------------------
    // Statements
    // -----------------------------------------------------------------------

    private void EmitBlock(Writer w, ShaderExpression body)
    {
        w.WriteLine("{");
        w.Indent();
        if (body is BlockExpression b)
        {
            foreach (var s in b.Body) EmitStatement(w, s);
        }
        else
        {
            EmitStatement(w, body);
        }
        w.Dedent();
        w.WriteLine("}");
    }

    private void EmitStatement(Writer w, ShaderExpression e)
    {
        switch (e)
        {
            case BlockExpression b:
                w.WriteLine("{");
                w.Indent();
                foreach (var s in b.Body) EmitStatement(w, s);
                w.Dedent();
                w.WriteLine("}");
                break;

            case DeclareLocalExpression d:
                EmitDeclareLocal(w, d);
                break;

            case AssignExpression a:
                EmitExpression(w, a.Target);
                w.Write(" = ");
                EmitExpression(w, a.Value);
                w.WriteLine(";");
                break;

            case IfExpression iff:
                w.Write("if (");
                EmitExpression(w, iff.Test);
                w.Write(") ");
                EmitBlock(w, iff.IfTrue);
                if (iff.IfFalse is { } elseArm)
                {
                    w.Write("else ");
                    if (elseArm is IfExpression) // else-if chain
                    {
                        EmitStatement(w, elseArm);
                    }
                    else
                    {
                        EmitBlock(w, elseArm);
                    }
                }
                break;

            case ForExpression fe:
                EmitFor(w, fe);
                break;

            case WhileExpression we:
                w.Write("while (");
                EmitExpression(w, we.Test);
                w.Write(") ");
                EmitBlock(w, we.Body);
                break;

            case ReturnExpression r:
                if (r.Value is null)
                {
                    w.WriteLine("return;");
                }
                else
                {
                    w.Write("return ");
                    EmitExpression(w, r.Value);
                    w.WriteLine(";");
                }
                break;

            case DiscardExpression:
                w.WriteLine("discard;");
                break;

            case BreakExpression:
                w.WriteLine("break;");
                break;

            case ContinueExpression:
                w.WriteLine("continue;");
                break;

            default:
                EmitExpression(w, e);
                w.WriteLine(";");
                break;
        }
    }

    private void EmitDeclareLocal(Writer w, DeclareLocalExpression d)
    {
        var v = d.Variable;
        var type = RequireType(v);
        // GLSL has no `const` for arbitrary locals beyond compile-time-constant
        // initializers; emit immutable lets as plain locals (GLSL semantics
        // permit reassignment but the IR forbids it earlier).
        w.Write($"{TypeDecl(type, v.Name)}");
        if (d.Initializer is { } init)
        {
            w.Write(" = ");
            EmitExpression(w, init);
        }
        w.WriteLine(";");
    }

    private void EmitFor(Writer w, ForExpression f)
    {
        var v = f.Variable;
        var type = RequireType(v);
        w.Write($"for ({TypeName(type)} {v.Name} = ");
        EmitExpression(w, f.Initial);
        w.Write("; ");
        EmitExpression(w, f.Test);
        w.Write("; ");
        // Step is a value expression, but GLSL wants a statement-form expression.
        // Most steps are assignments; if so emit `target = value` (no parens).
        if (f.Step is AssignExpression sa)
        {
            EmitExpression(w, sa.Target);
            w.Write(" = ");
            EmitExpression(w, sa.Value);
        }
        else
        {
            EmitExpression(w, f.Step);
        }
        w.Write(") ");
        EmitBlock(w, f.Body);
    }

    // -----------------------------------------------------------------------
    // Expressions
    // -----------------------------------------------------------------------

    private void EmitExpression(Writer w, ShaderExpression e)
    {
        switch (e)
        {
            case LiteralExpression lit:
                w.Write(FormatLiteral(lit));
                break;

            case ParameterExpression p:
                w.Write(p.Name);
                break;

            case GlobalReferenceExpression g:
                w.Write(GlobalRefName(g));
                break;

            case BinaryExpression b:
                w.Write("(");
                EmitExpression(w, b.Left);
                w.Write($" {BinaryOpToken(b.Op)} ");
                EmitExpression(w, b.Right);
                w.Write(")");
                break;

            case UnaryExpression u:
                w.Write("(");
                w.Write(UnaryOpToken(u.Op));
                EmitExpression(w, u.Operand);
                w.Write(")");
                break;

            case ConstructExpression ctor:
                w.Write(ConstructorTypeName(RequireType(ctor)));
                w.Write("(");
                for (int i = 0; i < ctor.Args.Length; i++)
                {
                    if (i > 0) w.Write(", ");
                    EmitExpression(w, ctor.Args[i]);
                }
                w.Write(")");
                break;

            case SwizzleExpression sw:
                EmitExpression(w, sw.Source);
                w.Write(".");
                w.Write(sw.Components);
                break;

            case IndexExpression idx:
                EmitExpression(w, idx.Source);
                w.Write("[");
                EmitExpression(w, idx.Index);
                w.Write("]");
                break;

            case FieldAccessExpression fa:
                EmitExpression(w, fa.Source);
                w.Write(".");
                w.Write(fa.FieldName);
                break;

            case CallExpression call:
                EmitCall(w, call);
                break;

            case SampleExpression samp:
                EmitSample(w, samp);
                break;

            case AssignExpression a:
                w.Write("(");
                EmitExpression(w, a.Target);
                w.Write(" = ");
                EmitExpression(w, a.Value);
                w.Write(")");
                break;

            case IfExpression iff when iff.IfFalse is not null:
                w.Write("(");
                EmitExpression(w, iff.Test);
                w.Write(" ? ");
                EmitExpression(w, iff.IfTrue);
                w.Write(" : ");
                EmitExpression(w, iff.IfFalse);
                w.Write(")");
                break;

            case BlockExpression:
                throw new InvalidOperationException(
                    "GLSL has no block expression form; lift the block into statement position.");

            case IfExpression:
                throw new InvalidOperationException(
                    "If used as a value expression must have an else branch.");

            default:
                throw new InvalidOperationException(
                    $"Cannot emit {e.GetType().Name} in expression position.");
        }
    }

    private void EmitCall(Writer w, CallExpression call)
    {
        switch (call.Target)
        {
            case IntrinsicCallTarget i:
                EmitIntrinsicCall(w, i.Op, call.Args);
                break;
            case UserFunctionCallTarget u:
                w.Write(u.Function.Name);
                w.Write("(");
                for (int k = 0; k < call.Args.Length; k++)
                {
                    if (k > 0) w.Write(", ");
                    EmitExpression(w, call.Args[k]);
                }
                w.Write(")");
                break;
            default:
                throw new InvalidOperationException($"Unknown call target {call.Target.GetType().Name}.");
        }
    }

    private void EmitIntrinsicCall(Writer w, ShaderIntrinsic op, ImmutableArray<ShaderExpression> args)
    {
        // Saturate has no GLSL builtin -- expand to clamp(x, 0.0, 1.0).
        if (op == ShaderIntrinsic.Saturate && args.Length == 1)
        {
            w.Write("clamp(");
            EmitExpression(w, args[0]);
            w.Write(", 0.0, 1.0)");
            return;
        }

        // As* depend on the argument's component type.
        if (op is ShaderIntrinsic.AsFloat or ShaderIntrinsic.AsInt or ShaderIntrinsic.AsUInt && args.Length == 1)
        {
            var argType = RequireType(args[0]);
            var name = BitCastIntrinsicName(op, argType);
            w.Write(name);
            w.Write("(");
            EmitExpression(w, args[0]);
            w.Write(")");
            return;
        }

        w.Write(IntrinsicName(op));
        w.Write("(");
        for (int k = 0; k < args.Length; k++)
        {
            if (k > 0) w.Write(", ");
            EmitExpression(w, args[k]);
        }
        w.Write(")");
    }

    private void EmitSample(Writer w, SampleExpression s)
    {
        // texture(sampler2D(tex, samp), uv) for implicit lod.
        // textureLod(sampler2D(tex, samp), uv, lod) for explicit.
        var texType = RequireType(s.Texture);
        var combined = texType switch
        {
            Texture2DType      => "sampler2D",
            Texture3DType      => "sampler3D",
            TextureCubeType    => "samplerCube",
            Texture2DArrayType => "sampler2DArray",
            _ => throw new InvalidOperationException(
                $"Sample target type {texType.GetType().Name} is not a texture."),
        };

        if (s.Lod is null)
        {
            w.Write("texture(");
        }
        else
        {
            w.Write("textureLod(");
        }
        w.Write(combined);
        w.Write("(");
        EmitExpression(w, s.Texture);
        w.Write(", ");
        EmitExpression(w, s.Sampler);
        w.Write("), ");
        EmitExpression(w, s.Coord);
        if (s.Lod is { } lod)
        {
            w.Write(", ");
            EmitExpression(w, lod);
        }
        w.Write(")");
    }

    // -----------------------------------------------------------------------
    // Names / formatting
    // -----------------------------------------------------------------------

    private static string GlobalRefName(GlobalReferenceExpression g)
    {
        var resolved = g.Global ?? throw new InvalidOperationException(
            $"Unbound global reference '{g.Name}'.");
        return resolved.Builtin switch
        {
            ShaderBuiltin.None          => resolved.Name,
            ShaderBuiltin.VertexIndex   => "gl_VertexIndex",
            ShaderBuiltin.InstanceIndex => "gl_InstanceIndex",
            ShaderBuiltin.Position      => "gl_Position",
            ShaderBuiltin.PointSize     => "gl_PointSize",
            ShaderBuiltin.FragCoord     => "gl_FragCoord",
            ShaderBuiltin.FrontFacing   => "gl_FrontFacing",
            ShaderBuiltin.FragDepth     => "gl_FragDepth",
            _ => throw new InvalidOperationException($"Unknown built-in {resolved.Builtin}."),
        };
    }

    private static string BinaryOpToken(ShaderBinaryOp op) => op switch
    {
        ShaderBinaryOp.Add    => "+",
        ShaderBinaryOp.Sub    => "-",
        ShaderBinaryOp.Mul    => "*",
        ShaderBinaryOp.Div    => "/",
        ShaderBinaryOp.Rem    => "%",
        ShaderBinaryOp.MatMul => "*",
        ShaderBinaryOp.Eq     => "==",
        ShaderBinaryOp.Ne     => "!=",
        ShaderBinaryOp.Lt     => "<",
        ShaderBinaryOp.Le     => "<=",
        ShaderBinaryOp.Gt     => ">",
        ShaderBinaryOp.Ge     => ">=",
        ShaderBinaryOp.And    => "&&",
        ShaderBinaryOp.Or     => "||",
        ShaderBinaryOp.BitAnd => "&",
        ShaderBinaryOp.BitOr  => "|",
        ShaderBinaryOp.BitXor => "^",
        ShaderBinaryOp.Shl    => "<<",
        ShaderBinaryOp.Shr    => ">>",
        _ => throw new InvalidOperationException($"Unknown binary op {op}."),
    };

    private static string UnaryOpToken(ShaderUnaryOp op) => op switch
    {
        ShaderUnaryOp.Neg    => "-",
        ShaderUnaryOp.Not    => "!",
        ShaderUnaryOp.BitNot => "~",
        _ => throw new InvalidOperationException($"Unknown unary op {op}."),
    };

    private static string IntrinsicName(ShaderIntrinsic op) => op switch
    {
        ShaderIntrinsic.Abs         => "abs",
        ShaderIntrinsic.Sign        => "sign",
        ShaderIntrinsic.Floor       => "floor",
        ShaderIntrinsic.Ceil        => "ceil",
        ShaderIntrinsic.Round       => "round",
        ShaderIntrinsic.Trunc       => "trunc",
        ShaderIntrinsic.Frac        => "fract",
        ShaderIntrinsic.Mod         => "mod",
        ShaderIntrinsic.Sin         => "sin",
        ShaderIntrinsic.Cos         => "cos",
        ShaderIntrinsic.Tan         => "tan",
        ShaderIntrinsic.Asin        => "asin",
        ShaderIntrinsic.Acos        => "acos",
        ShaderIntrinsic.Atan        => "atan",
        ShaderIntrinsic.Atan2       => "atan",
        ShaderIntrinsic.Exp         => "exp",
        ShaderIntrinsic.Exp2        => "exp2",
        ShaderIntrinsic.Log         => "log",
        ShaderIntrinsic.Log2        => "log2",
        ShaderIntrinsic.Pow         => "pow",
        ShaderIntrinsic.Sqrt        => "sqrt",
        ShaderIntrinsic.InverseSqrt => "inversesqrt",
        ShaderIntrinsic.Min         => "min",
        ShaderIntrinsic.Max         => "max",
        ShaderIntrinsic.Clamp       => "clamp",
        ShaderIntrinsic.Saturate    => "clamp", // handled specially above
        ShaderIntrinsic.Mix         => "mix",
        ShaderIntrinsic.Step        => "step",
        ShaderIntrinsic.SmoothStep  => "smoothstep",
        ShaderIntrinsic.Dot         => "dot",
        ShaderIntrinsic.Cross       => "cross",
        ShaderIntrinsic.Length      => "length",
        ShaderIntrinsic.Distance    => "distance",
        ShaderIntrinsic.Normalize   => "normalize",
        ShaderIntrinsic.Reflect     => "reflect",
        ShaderIntrinsic.Refract     => "refract",
        ShaderIntrinsic.Transpose   => "transpose",
        ShaderIntrinsic.Determinant => "determinant",
        ShaderIntrinsic.Inverse     => "inverse",
        ShaderIntrinsic.Ddx         => "dFdx",
        ShaderIntrinsic.Ddy         => "dFdy",
        ShaderIntrinsic.FWidth      => "fwidth",
        ShaderIntrinsic.AsFloat     => "intBitsToFloat", // arg-dependent; resolved by BitCastIntrinsicName
        ShaderIntrinsic.AsInt       => "floatBitsToInt",
        ShaderIntrinsic.AsUInt      => "floatBitsToUint",
        _ => throw new InvalidOperationException($"Unknown intrinsic {op}."),
    };

    private static string BitCastIntrinsicName(ShaderIntrinsic op, ShaderType argType)
    {
        var component = ComponentScalar(argType);
        return (op, component) switch
        {
            (ShaderIntrinsic.AsFloat, IntType)  => "intBitsToFloat",
            (ShaderIntrinsic.AsFloat, UIntType) => "uintBitsToFloat",
            (ShaderIntrinsic.AsInt,   FloatType) => "floatBitsToInt",
            (ShaderIntrinsic.AsUInt,  FloatType) => "floatBitsToUint",
            _ => throw new InvalidOperationException(
                $"Bit-cast {op} not supported from {argType.GetType().Name}."),
        };
    }

    private static ShaderType ComponentScalar(ShaderType t) => t switch
    {
        VectorType v => v.Component,
        _            => t,
    };

    // -----------------------------------------------------------------------
    // Type emission
    // -----------------------------------------------------------------------

    private static string TypeName(ShaderType t) => t switch
    {
        VoidType  => "void",
        BoolType  => "bool",
        IntType   => "int",
        UIntType  => "uint",
        FloatType => "float",
        VectorType v => VectorTypeName(v),
        MatrixType m => MatrixTypeName(m),
        ArrayType a  => $"{TypeName(a.Element)}[{a.Length}]", // valid in expression position
        StructType s => s.Name,
        Texture2DType      => "texture2D",
        Texture3DType      => "texture3D",
        TextureCubeType    => "textureCube",
        Texture2DArrayType => "texture2DArray",
        SamplerType        => "sampler",
        _ => throw new InvalidOperationException($"Unknown type {t.GetType().Name}."),
    };

    /// <summary>Type name as it appears in a constructor: <c>vec3(...)</c>, <c>float[4](...)</c>, etc.</summary>
    private static string ConstructorTypeName(ShaderType t) => TypeName(t);

    /// <summary>
    /// Type name with a declarator: emits the suffixed-array form
    /// <c>float foo[8]</c> for arrays. For non-array types, equivalent to
    /// <c>"{TypeName} {name}"</c>.
    /// </summary>
    private static string TypeDecl(ShaderType t, string name) => t is ArrayType a
        ? $"{TypeName(a.Element)} {name}[{a.Length}]"
        : $"{TypeName(t)} {name}";

    private static string TextureTypeName(ShaderType t) => t switch
    {
        Texture2DType      => "texture2D",
        Texture3DType      => "texture3D",
        TextureCubeType    => "textureCube",
        Texture2DArrayType => "texture2DArray",
        _ => throw new InvalidOperationException($"Not a texture type: {t.GetType().Name}."),
    };

    private static string VectorTypeName(VectorType v)
    {
        var prefix = v.Component switch
        {
            BoolType  => "b",
            IntType   => "i",
            UIntType  => "u",
            FloatType => "",
            _ => throw new InvalidOperationException(
                $"Vector component type {v.Component.GetType().Name} has no GLSL form."),
        };
        return $"{prefix}vec{v.N}";
    }

    private static string MatrixTypeName(MatrixType m)
    {
        if (m.Component is not FloatType)
            throw new InvalidOperationException("GLSL matrices must be float-typed.");
        // GLSL: matCxR means C columns, R rows. Square aliases as matN.
        return m.Cols == m.Rows ? $"mat{m.Cols}" : $"mat{m.Cols}x{m.Rows}";
    }

    // -----------------------------------------------------------------------
    // Literals
    // -----------------------------------------------------------------------

    private static string FormatLiteral(LiteralExpression lit)
    {
        var t = RequireType(lit);
        return (t, lit.Value) switch
        {
            (BoolType,  bool b) => b ? "true" : "false",
            (IntType,   int i)  => i.ToString(CultureInfo.InvariantCulture),
            (UIntType,  uint u) => u.ToString(CultureInfo.InvariantCulture) + "u",
            (FloatType, float f) => FormatFloat(f),
            _ => throw new InvalidOperationException(
                $"Literal value {lit.Value} ({lit.Value?.GetType().Name}) does not match type {t.GetType().Name}."),
        };
    }

    private static string FormatFloat(float f)
    {
        if (float.IsNaN(f))              return "(0.0/0.0)";
        if (float.IsPositiveInfinity(f)) return "(1.0/0.0)";
        if (float.IsNegativeInfinity(f)) return "(-1.0/0.0)";
        var s = f.ToString("R", CultureInfo.InvariantCulture);
        if (s.IndexOfAny(['.', 'e', 'E']) < 0) s += ".0";
        return s;
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static ShaderType RequireType(ShaderExpression e)
        => e.ResultType ?? throw new InvalidOperationException(
            $"Expression {e.GetType().Name} has no result type; bind the set before emitting.");

    // -----------------------------------------------------------------------
    // Indented writer
    // -----------------------------------------------------------------------

    private sealed class Writer
    {
        private readonly StringBuilder _sb = new();
        private int _indent;
        private bool _atLineStart = true;

        public void Indent() => _indent++;
        public void Dedent() => _indent--;

        public void Write(string s)
        {
            if (_atLineStart)
            {
                _sb.Append(' ', _indent * 4);
                _atLineStart = false;
            }
            _sb.Append(s);
        }

        public void WriteLine(string s = "")
        {
            if (s.Length > 0) Write(s);
            else if (_atLineStart) { /* no indent on blank line */ }
            _sb.Append('\n');
            _atLineStart = true;
        }

        public override string ToString() => _sb.ToString();
    }
}
