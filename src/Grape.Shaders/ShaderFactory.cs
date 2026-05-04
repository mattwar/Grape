namespace Grape.Shaders;

/// <summary>
/// Constructs unbound IR nodes. Performs no type inference, name resolution,
/// or shape validation -- it is a thin, trivial constructor surface.
/// Resolving the produced tree (filling in result types, resolving global
/// references, etc.) is the job of a <see cref="ShaderRewriter"/> binding
/// pass.
/// </summary>
public static class ShaderFactory
{
    // ---- Literals -----------------------------------------------------------

    public static LiteralExpression Const(bool v)  => new(ShaderTypeSystem.Bool,  v);
    public static LiteralExpression Const(int v)   => new(ShaderTypeSystem.Int,   v);
    public static LiteralExpression Const(uint v)  => new(ShaderTypeSystem.UInt,  v);
    public static LiteralExpression Const(float v) => new(ShaderTypeSystem.Float, v);

    // ---- Bindings -----------------------------------------------------------

    public static ParameterExpression Parameter(string name, ShaderType type) => new(name, type);

    /// <summary>Per-vertex attribute read from a vertex buffer (vertex stage only).</summary>
    public static ShaderGlobal VertexInput(string name, ShaderType type)
        => new(name, type, ShaderGlobalKind.VertexInput);

    /// <summary>Varying received from the previous pipeline stage, interpolated per-invocation.</summary>
    public static ShaderGlobal StageInput(string name, ShaderType type)
        => new(name, type, ShaderGlobalKind.StageInput);

    /// <summary>Value written by this stage and consumed by the next stage (or a render target, in the fragment stage).</summary>
    public static ShaderGlobal StageOutput(string name, ShaderType type)
        => new(name, type, ShaderGlobalKind.StageOutput);

    /// <summary>Read-only buffer-backed value uploaded by the CPU and constant for the entire draw.</summary>
    public static ShaderGlobal Uniform(string name, ShaderType type)
        => new(name, type, ShaderGlobalKind.Uniform);

    /// <summary>Tiny constant block stored directly in the command buffer; cheaper than a uniform but size-limited.</summary>
    public static ShaderGlobal PushConstant(string name, ShaderType type)
        => new(name, type, ShaderGlobalKind.PushConstant);

    /// <summary>Sampled image resource; combined with a <see cref="Sampler"/> to read filtered texels.</summary>
    public static ShaderGlobal Texture(string name, ShaderType type)
        => new(name, type, ShaderGlobalKind.Texture);

    /// <summary>Filtering / addressing state used to sample a texture; bound separately from the texture itself.</summary>
    public static ShaderGlobal Sampler(string name)
        => new(name, ShaderTypeSystem.Sampler, ShaderGlobalKind.Sampler);

    /// <summary>Pipeline-defined variable like <c>gl_Position</c> or <c>gl_FragCoord</c>; written or read by the GPU itself, not laid out by location or binding.</summary>
    public static ShaderGlobal Builtin(ShaderBuiltin builtin, ShaderGlobalKind direction, ShaderType type)
        => new(builtin.ToString(), type, direction, builtin);

    public static GlobalReferenceExpression Ref(ShaderGlobal global) => new(global);
    public static GlobalReferenceExpression Ref(string name) => new(name);

    // ---- Value-producing expressions ---------------------------------------

    public static BinaryExpression Binary(ShaderBinaryOp op, ShaderExpression left, ShaderExpression right)
        => new(op, left, right);

    public static BinaryExpression Add(ShaderExpression left, ShaderExpression right) => new(ShaderBinaryOp.Add, left, right);
    public static BinaryExpression Sub(ShaderExpression left, ShaderExpression right) => new(ShaderBinaryOp.Sub, left, right);
    public static BinaryExpression Mul(ShaderExpression left, ShaderExpression right) => new(ShaderBinaryOp.Mul, left, right);
    public static BinaryExpression Div(ShaderExpression left, ShaderExpression right) => new(ShaderBinaryOp.Div, left, right);
    public static BinaryExpression Rem(ShaderExpression left, ShaderExpression right) => new(ShaderBinaryOp.Rem, left, right);
    public static BinaryExpression MatMul(ShaderExpression left, ShaderExpression right) => new(ShaderBinaryOp.MatMul, left, right);

    public static BinaryExpression Eq(ShaderExpression left, ShaderExpression right) => new(ShaderBinaryOp.Eq, left, right);
    public static BinaryExpression Ne(ShaderExpression left, ShaderExpression right) => new(ShaderBinaryOp.Ne, left, right);
    public static BinaryExpression Lt(ShaderExpression left, ShaderExpression right) => new(ShaderBinaryOp.Lt, left, right);
    public static BinaryExpression Le(ShaderExpression left, ShaderExpression right) => new(ShaderBinaryOp.Le, left, right);
    public static BinaryExpression Gt(ShaderExpression left, ShaderExpression right) => new(ShaderBinaryOp.Gt, left, right);
    public static BinaryExpression Ge(ShaderExpression left, ShaderExpression right) => new(ShaderBinaryOp.Ge, left, right);

    public static BinaryExpression And(ShaderExpression left, ShaderExpression right) => new(ShaderBinaryOp.And, left, right);
    public static BinaryExpression Or(ShaderExpression left, ShaderExpression right) => new(ShaderBinaryOp.Or, left, right);

    public static BinaryExpression BitAnd(ShaderExpression left, ShaderExpression right) => new(ShaderBinaryOp.BitAnd, left, right);
    public static BinaryExpression BitOr(ShaderExpression left, ShaderExpression right) => new(ShaderBinaryOp.BitOr, left, right);
    public static BinaryExpression BitXor(ShaderExpression left, ShaderExpression right) => new(ShaderBinaryOp.BitXor, left, right);
    public static BinaryExpression Shl(ShaderExpression left, ShaderExpression right) => new(ShaderBinaryOp.Shl, left, right);
    public static BinaryExpression Shr(ShaderExpression left, ShaderExpression right) => new(ShaderBinaryOp.Shr, left, right);

    public static UnaryExpression Unary(ShaderUnaryOp op, ShaderExpression operand) => new(op, operand);

    public static UnaryExpression Neg(ShaderExpression operand) => new(ShaderUnaryOp.Neg, operand);
    public static UnaryExpression Not(ShaderExpression operand) => new(ShaderUnaryOp.Not, operand);
    public static UnaryExpression BitNot(ShaderExpression operand) => new(ShaderUnaryOp.BitNot, operand);

    public static ConstructExpression Construct(ShaderType type, params ShaderExpression[] args)
        => new(type, [.. args]);

    public static ConstructExpression Splat(ShaderType vectorType, ShaderExpression scalar)
        => new(vectorType, [scalar]);

    public static SwizzleExpression Swizzle(ShaderExpression source, string components)
        => new(source, components);

    public static IndexExpression Index(ShaderExpression source, ShaderExpression index)
        => new(source, index);

    public static FieldAccessExpression Field(ShaderExpression source, string fieldName)
        => new(source, fieldName);

    public static CallExpression Call(ShaderIntrinsic op, params ShaderExpression[] args)
        => new(new IntrinsicCallTarget(op), [.. args]);

    public static CallExpression Call(ShaderFunction function, params ShaderExpression[] args)
        => new(new UserFunctionCallTarget(function), [.. args]);

    // Componentwise math (unary).
    public static CallExpression Abs(ShaderExpression x)         => Call(ShaderIntrinsic.Abs, x);
    public static CallExpression Sign(ShaderExpression x)        => Call(ShaderIntrinsic.Sign, x);
    public static CallExpression Floor(ShaderExpression x)       => Call(ShaderIntrinsic.Floor, x);
    public static CallExpression Ceil(ShaderExpression x)        => Call(ShaderIntrinsic.Ceil, x);
    public static CallExpression Round(ShaderExpression x)       => Call(ShaderIntrinsic.Round, x);
    public static CallExpression Trunc(ShaderExpression x)       => Call(ShaderIntrinsic.Trunc, x);
    public static CallExpression Frac(ShaderExpression x)        => Call(ShaderIntrinsic.Frac, x);
    public static CallExpression Sin(ShaderExpression x)         => Call(ShaderIntrinsic.Sin, x);
    public static CallExpression Cos(ShaderExpression x)         => Call(ShaderIntrinsic.Cos, x);
    public static CallExpression Tan(ShaderExpression x)         => Call(ShaderIntrinsic.Tan, x);
    public static CallExpression Asin(ShaderExpression x)        => Call(ShaderIntrinsic.Asin, x);
    public static CallExpression Acos(ShaderExpression x)        => Call(ShaderIntrinsic.Acos, x);
    public static CallExpression Atan(ShaderExpression x)        => Call(ShaderIntrinsic.Atan, x);
    public static CallExpression Exp(ShaderExpression x)         => Call(ShaderIntrinsic.Exp, x);
    public static CallExpression Exp2(ShaderExpression x)        => Call(ShaderIntrinsic.Exp2, x);
    public static CallExpression Log(ShaderExpression x)         => Call(ShaderIntrinsic.Log, x);
    public static CallExpression Log2(ShaderExpression x)        => Call(ShaderIntrinsic.Log2, x);
    public static CallExpression Sqrt(ShaderExpression x)        => Call(ShaderIntrinsic.Sqrt, x);
    public static CallExpression InverseSqrt(ShaderExpression x) => Call(ShaderIntrinsic.InverseSqrt, x);
    public static CallExpression Saturate(ShaderExpression x)    => Call(ShaderIntrinsic.Saturate, x);

    // Componentwise math (binary).
    public static CallExpression Mod(ShaderExpression x, ShaderExpression y)            => Call(ShaderIntrinsic.Mod, x, y);
    public static CallExpression Atan2(ShaderExpression y, ShaderExpression x)          => Call(ShaderIntrinsic.Atan2, y, x);
    public static CallExpression Pow(ShaderExpression x, ShaderExpression y)            => Call(ShaderIntrinsic.Pow, x, y);
    public static CallExpression Min(ShaderExpression a, ShaderExpression b)            => Call(ShaderIntrinsic.Min, a, b);
    public static CallExpression Max(ShaderExpression a, ShaderExpression b)            => Call(ShaderIntrinsic.Max, a, b);
    public static CallExpression Step(ShaderExpression edge, ShaderExpression x)        => Call(ShaderIntrinsic.Step, edge, x);

    // Componentwise math (ternary).
    public static CallExpression Clamp(ShaderExpression x, ShaderExpression lo, ShaderExpression hi) => Call(ShaderIntrinsic.Clamp, x, lo, hi);
    public static CallExpression Mix(ShaderExpression a, ShaderExpression b, ShaderExpression t)     => Call(ShaderIntrinsic.Mix, a, b, t);
    public static CallExpression SmoothStep(ShaderExpression edge0, ShaderExpression edge1, ShaderExpression x) => Call(ShaderIntrinsic.SmoothStep, edge0, edge1, x);

    // Vector.
    public static CallExpression Dot(ShaderExpression a, ShaderExpression b)            => Call(ShaderIntrinsic.Dot, a, b);
    public static CallExpression Cross(ShaderExpression a, ShaderExpression b)          => Call(ShaderIntrinsic.Cross, a, b);
    public static CallExpression Length(ShaderExpression v)                             => Call(ShaderIntrinsic.Length, v);
    public static CallExpression Distance(ShaderExpression a, ShaderExpression b)       => Call(ShaderIntrinsic.Distance, a, b);
    public static CallExpression Normalize(ShaderExpression v)                          => Call(ShaderIntrinsic.Normalize, v);
    public static CallExpression Reflect(ShaderExpression i, ShaderExpression n)        => Call(ShaderIntrinsic.Reflect, i, n);
    public static CallExpression Refract(ShaderExpression i, ShaderExpression n, ShaderExpression eta) => Call(ShaderIntrinsic.Refract, i, n, eta);

    // Matrix.
    public static CallExpression Transpose(ShaderExpression m)   => Call(ShaderIntrinsic.Transpose, m);
    public static CallExpression Determinant(ShaderExpression m) => Call(ShaderIntrinsic.Determinant, m);
    public static CallExpression Inverse(ShaderExpression m)     => Call(ShaderIntrinsic.Inverse, m);

    // Derivatives (fragment only).
    public static CallExpression Ddx(ShaderExpression x)    => Call(ShaderIntrinsic.Ddx, x);
    public static CallExpression Ddy(ShaderExpression x)    => Call(ShaderIntrinsic.Ddy, x);
    public static CallExpression FWidth(ShaderExpression x) => Call(ShaderIntrinsic.FWidth, x);

    // Bit-casts.
    public static CallExpression AsFloat(ShaderExpression x) => Call(ShaderIntrinsic.AsFloat, x);
    public static CallExpression AsInt(ShaderExpression x)   => Call(ShaderIntrinsic.AsInt, x);
    public static CallExpression AsUInt(ShaderExpression x)  => Call(ShaderIntrinsic.AsUInt, x);

    public static SampleExpression Sample(
        ShaderExpression texture,
        ShaderExpression sampler,
        ShaderExpression coord,
        ShaderExpression? lod = null)
        => new(texture, sampler, coord, lod);

    // ---- Statement-shaped expressions --------------------------------------

    public static BlockExpression Block(params ShaderExpression[] body) => new([.. body]);

    public static DeclareLocalExpression Let(ParameterExpression variable, ShaderExpression init)
        => new(variable, init, isMutable: false);

    public static DeclareLocalExpression Var(ParameterExpression variable, ShaderExpression? init = null)
        => new(variable, init, isMutable: true);

    /// <summary>
    /// Introduces an immutable local with <paramref name="init"/> and invokes
    /// <paramref name="body"/> with the local in scope. Returns a block that
    /// declares the local and then evaluates the body.
    /// </summary>
    public static BlockExpression Let(
        ParameterExpression variable,
        ShaderExpression init,
        Func<ParameterExpression, ShaderExpression> body)
        => new([new DeclareLocalExpression(variable, init, isMutable: false), body(variable)]);

    /// <summary>
    /// Introduces a mutable local (optionally initialized) and invokes
    /// <paramref name="body"/> with the local in scope. Returns a block that
    /// declares the local and then evaluates the body.
    /// </summary>
    public static BlockExpression Var(
        ParameterExpression variable,
        ShaderExpression? init,
        Func<ParameterExpression, ShaderExpression> body)
        => new([new DeclareLocalExpression(variable, init, isMutable: true), body(variable)]);

    public static AssignExpression Assign(ShaderExpression target, ShaderExpression value)
        => new(target, value);

    public static IfExpression If(ShaderExpression test, ShaderExpression then) => new(test, then);
    public static IfExpression If(ShaderExpression test, ShaderExpression then, ShaderExpression @else)
        => new(test, then, @else);

    public static ForExpression For(
        ParameterExpression variable,
        ShaderExpression initial,
        ShaderExpression test,
        ShaderExpression step,
        ShaderExpression body)
        => new(variable, initial, test, step, body);

    /// <summary>
    /// Bounded for-loop where <paramref name="test"/>, <paramref name="step"/>,
    /// and <paramref name="body"/> are lambdas that receive the loop variable.
    /// </summary>
    public static ForExpression For(
        ParameterExpression variable,
        ShaderExpression initial,
        Func<ParameterExpression, ShaderExpression> test,
        Func<ParameterExpression, ShaderExpression> step,
        Func<ParameterExpression, ShaderExpression> body)
        => new(variable, initial, test(variable), step(variable), body(variable));

    public static WhileExpression While(ShaderExpression test, ShaderExpression body) => new(test, body);

    public static ReturnExpression Return(ShaderExpression? value = null) => new(value);
    public static DiscardExpression Discard()  => new();
    public static BreakExpression   Break()    => new();
    public static ContinueExpression Continue() => new();

    // ---- Set-level ----------------------------------------------------------

    public static ShaderFunction Function(
        string name,
        ShaderType returnType,
        ImmutableArray<ParameterExpression> parameters,
        ShaderExpression body)
        => new(name, returnType, parameters, body);


    public static ShaderFunction Function(
        string name,
        ShaderType returnType,
        ShaderExpression body)
        => new(name, returnType, [.. Array.Empty<ParameterExpression>()], body);

    public static ShaderFunction Function(
        string name,
        ShaderType returnType,
        ParameterExpression param1,
        Func<ParameterExpression, ShaderExpression> bodyFunc) 
        =>
        new(name, returnType, [param1], bodyFunc(param1));

    public static ShaderFunction Function(
        string name,
        ShaderType returnType,
        ParameterExpression param1,
        ParameterExpression param2,
        Func<ParameterExpression, ParameterExpression, ShaderExpression> bodyFunc) 
        =>
        new(name, returnType, [param1, param2], bodyFunc(param1, param2));

    public static ShaderFunction Function(
        string name,
        ShaderType returnType,
        ParameterExpression param1,
        ParameterExpression param2,
        ParameterExpression param3,
        Func<ParameterExpression, ParameterExpression, ParameterExpression, ShaderExpression> bodyFunc) 
        =>
        new(name, returnType, [param1, param2, param3], bodyFunc(param1, param2, param3));

    public static ShaderFunction Function(
        string name,
        ShaderType returnType,
        ParameterExpression param1,
        ParameterExpression param2,
        ParameterExpression param3,
        ParameterExpression param4,
        Func<ParameterExpression, ParameterExpression, ParameterExpression, ParameterExpression, ShaderExpression> bodyFunc) 
        =>
        new(name, returnType, [param1, param2, param3, param4], bodyFunc(param1, param2, param3, param4));

    public static ShaderStage Stage(
        ShaderStageKind kind,
        ImmutableArray<ShaderGlobal> globals,
        ImmutableArray<ShaderFunction> functions,
        ShaderExpression entryBody)
        => new(kind, globals, functions, entryBody);

    public static ShaderStage Stage(
        ShaderStageKind kind,
        ShaderGlobal global1,
        Func<ShaderGlobal, ShaderExpression> entryBodyFn)
        => Stage(kind, [global1], ImmutableArray<ShaderFunction>.Empty, entryBodyFn(global1));

    public static ShaderStage Stage(
        ShaderStageKind kind,
        ShaderGlobal global1,
        Func<ShaderGlobal, ImmutableArray<ShaderFunction>> funcsFn,
        Func<ShaderGlobal, ShaderExpression> entryBodyFn)
        => Stage(kind, [global1], funcsFn(global1), entryBodyFn(global1));

    public static ShaderStage Stage(
        ShaderStageKind kind,
        ShaderGlobal global1,
        ShaderGlobal global2,
        Func<ShaderGlobal, ShaderGlobal, ShaderExpression> entryBodyFn)
        => Stage(kind, [global1, global2], ImmutableArray<ShaderFunction>.Empty, entryBodyFn(global1, global2));

    public static ShaderStage Stage(
        ShaderStageKind kind,
        ShaderGlobal global1,
        ShaderGlobal global2,
        Func<ShaderGlobal, ShaderGlobal, ImmutableArray<ShaderFunction>> funcsFn,
        Func<ShaderGlobal, ShaderGlobal, ShaderExpression> entryBodyFn)
        => Stage(kind, [global1, global2], funcsFn(global1, global2), entryBodyFn(global1, global2));

    public static ShaderStage Stage(
        ShaderStageKind kind,
        ShaderGlobal global1,
        ShaderGlobal global2,
        ShaderGlobal global3,
        Func<ShaderGlobal, ShaderGlobal, ShaderGlobal, ShaderExpression> entryBodyFn)
        => Stage(kind, [global1, global2, global3], ImmutableArray<ShaderFunction>.Empty, entryBodyFn(global1, global2, global3));

    public static ShaderStage Stage(
        ShaderStageKind kind,
        ShaderGlobal global1,
        ShaderGlobal global2,
        ShaderGlobal global3,
        Func<ShaderGlobal, ShaderGlobal, ShaderGlobal, ImmutableArray<ShaderFunction>> funcsFn,
        Func<ShaderGlobal, ShaderGlobal, ShaderGlobal, ShaderExpression> entryBodyFn)
        => Stage(kind, [global1, global2, global3], funcsFn(global1, global2, global3), entryBodyFn(global1, global2, global3));

    public static ShaderStage Stage(
        ShaderStageKind kind,
        ShaderGlobal global1,
        ShaderGlobal global2,
        ShaderGlobal global3,
        ShaderGlobal global4,
        Func<ShaderGlobal, ShaderGlobal, ShaderGlobal, ShaderGlobal, ShaderExpression> entryBodyFn)
        => Stage(kind, [global1, global2, global3, global4], ImmutableArray<ShaderFunction>.Empty, entryBodyFn(global1, global2, global3, global4));

    public static ShaderStage Stage(
        ShaderStageKind kind,
        ShaderGlobal global1,
        ShaderGlobal global2,
        ShaderGlobal global3,
        ShaderGlobal global4,
        Func<ShaderGlobal, ShaderGlobal, ShaderGlobal, ShaderGlobal, ImmutableArray<ShaderFunction>> funcsFn,
        Func<ShaderGlobal, ShaderGlobal, ShaderGlobal, ShaderGlobal, ShaderExpression> entryBodyFn)
        => Stage(kind, [global1, global2, global3, global4], funcsFn(global1, global2, global3, global4), entryBodyFn(global1, global2, global3, global4));

    public static ShaderSet Set(
        ShaderStage? vertex, 
        ShaderStage? fragment, 
        ShaderStage? compute = null)
        => new(vertex, fragment, compute);
}
