namespace Grape.Shaders;

/// <summary>
/// Constructs unbound IR nodes. A <see cref="ShaderFactory"/> performs no
/// type inference, name resolution, or shape validation -- it is a thin,
/// trivial constructor surface. Resolving the produced tree (filling in
/// result types, resolving global references, etc.) is the job of a
/// <see cref="ShaderRewriter"/> binding pass.
/// </summary>
public sealed class ShaderFactory(ShaderTypeSystem types)
{
    /// <summary>Type system used by this factory for type construction (vector, matrix, etc.).</summary>
    public ShaderTypeSystem Types { get; } = types;

    /// <summary>A factory backed by a fresh <see cref="DefaultShaderTypeSystem"/>.</summary>
    public static ShaderFactory Default { get; } = new(new DefaultShaderTypeSystem());

    // ---- Literals -----------------------------------------------------------

    public LiteralExpression Const(bool v)  => new(ShaderTypeSystem.Bool,  v);
    public LiteralExpression Const(int v)   => new(ShaderTypeSystem.Int,   v);
    public LiteralExpression Const(uint v)  => new(ShaderTypeSystem.UInt,  v);
    public LiteralExpression Const(float v) => new(ShaderTypeSystem.Float, v);

    // ---- Bindings -----------------------------------------------------------

    public ParameterExpression Parameter(string name, ShaderType type) => new(name, type);

    public ShaderGlobal VertexInput(string name, ShaderType type)
        => new(name, type, ShaderGlobalKind.VertexInput);

    public ShaderGlobal StageInput(string name, ShaderType type)
        => new(name, type, ShaderGlobalKind.StageInput);

    public ShaderGlobal StageOutput(string name, ShaderType type)
        => new(name, type, ShaderGlobalKind.StageOutput);

    public ShaderGlobal Uniform(string name, ShaderType type)
        => new(name, type, ShaderGlobalKind.Uniform);

    public ShaderGlobal PushConstant(string name, ShaderType type)
        => new(name, type, ShaderGlobalKind.PushConstant);

    public ShaderGlobal Texture(string name, ShaderType type)
        => new(name, type, ShaderGlobalKind.Texture);

    public ShaderGlobal Sampler(string name)
        => new(name, ShaderTypeSystem.Sampler, ShaderGlobalKind.Sampler);

    public ShaderGlobal Builtin(ShaderBuiltin builtin, ShaderGlobalKind direction, ShaderType type)
        => new(builtin.ToString(), type, direction, builtin);

    public GlobalReferenceExpression Ref(ShaderGlobal global) => new(global);
    public GlobalReferenceExpression Ref(string name) => new(name);

    // ---- Value-producing expressions ---------------------------------------

    public BinaryExpression Binary(ShaderBinaryOp op, ShaderExpression left, ShaderExpression right)
        => new(op, left, right);

    public UnaryExpression Unary(ShaderUnaryOp op, ShaderExpression operand) => new(op, operand);

    public ConstructExpression Construct(ShaderType type, params ShaderExpression[] args)
        => new(type, [.. args]);

    public ConstructExpression Splat(ShaderType vectorType, ShaderExpression scalar)
        => new(vectorType, [scalar]);

    public SwizzleExpression Swizzle(ShaderExpression source, string components)
        => new(source, components);

    public IndexExpression Index(ShaderExpression source, ShaderExpression index)
        => new(source, index);

    public FieldAccessExpression Field(ShaderExpression source, string fieldName)
        => new(source, fieldName);

    public CallExpression Call(ShaderIntrinsic op, params ShaderExpression[] args)
        => new(new IntrinsicCallTarget(op), [.. args]);

    public CallExpression Call(ShaderFunction function, params ShaderExpression[] args)
        => new(new UserFunctionCallTarget(function), [.. args]);

    public SampleExpression Sample(
        ShaderExpression texture,
        ShaderExpression sampler,
        ShaderExpression coord,
        ShaderExpression? lod = null)
        => new(texture, sampler, coord, lod);

    // ---- Statement-shaped expressions --------------------------------------

    public BlockExpression Block(params ShaderExpression[] body) => new([.. body]);

    public DeclareLocalExpression Let(ParameterExpression variable, ShaderExpression init)
        => new(variable, init, isMutable: false);

    public DeclareLocalExpression Var(ParameterExpression variable, ShaderExpression? init = null)
        => new(variable, init, isMutable: true);

    public AssignExpression Assign(ShaderExpression target, ShaderExpression value)
        => new(target, value);

    public IfExpression If(ShaderExpression test, ShaderExpression then) => new(test, then);
    public IfExpression If(ShaderExpression test, ShaderExpression then, ShaderExpression @else)
        => new(test, then, @else);

    public ForExpression For(
        ParameterExpression variable,
        ShaderExpression initial,
        ShaderExpression test,
        ShaderExpression step,
        ShaderExpression body)
        => new(variable, initial, test, step, body);

    public WhileExpression While(ShaderExpression test, ShaderExpression body) => new(test, body);

    public ReturnExpression Return(ShaderExpression? value = null) => new(value);
    public DiscardExpression Discard()  => new();
    public BreakExpression   Break()    => new();
    public ContinueExpression Continue() => new();

    // ---- Module-level -------------------------------------------------------

    public ShaderFunction Function(
        string name,
        ShaderType returnType,
        ImmutableArray<ParameterExpression> parameters,
        ShaderExpression body)
        => new(name, returnType, parameters, body);

    public ShaderStage Stage(
        ShaderStageKind kind,
        ImmutableArray<ShaderGlobal> globals,
        ImmutableArray<ShaderFunction> functions,
        ShaderExpression entryBody)
        => new(kind, globals, functions, entryBody);

    public ShaderModule Module(ShaderStage? vertex, ShaderStage? fragment, ShaderStage? compute = null)
        => new(vertex, fragment, compute);
}
