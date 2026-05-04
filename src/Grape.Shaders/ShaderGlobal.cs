namespace Grape.Shaders;

public enum ShaderGlobalKind
{
    VertexInput,
    StageInput,
    StageOutput,
    Uniform,
    PushConstant,
    Texture,
    Sampler,
    Builtin,
}

/// <summary>
/// A module-level binding: name, type, role, and (when relevant) the built-in
/// it represents. Layout numbers (<see cref="Location"/>, <see cref="BindingSet"/>,
/// <see cref="BindingSlot"/>) are nullable and assigned monotonically by a
/// layout-pass rewriter.
/// </summary>
public sealed class ShaderGlobal : ShaderElement
{
    public string Name { get; }
    public ShaderType Type { get; }
    public ShaderGlobalKind GlobalKind { get; }
    public ShaderBuiltin Builtin { get; }
    public int? Location { get; }
    public int? BindingSet { get; }
    public int? BindingSlot { get; }

    public ShaderGlobal(
        string name,
        ShaderType type,
        ShaderGlobalKind globalKind,
        ShaderBuiltin builtin = ShaderBuiltin.None)
        : this(name, type, globalKind, builtin, null, null, null, null) { }

    private ShaderGlobal(
        string name,
        ShaderType type,
        ShaderGlobalKind globalKind,
        ShaderBuiltin builtin,
        int? location,
        int? bindingSet,
        int? bindingSlot,
        ImmutableList<ShaderDiagnostic>? diagnostics)
        : base(ContainsState.None, diagnostics)
    {
        Name = name;
        Type = type;
        GlobalKind = globalKind;
        Builtin = builtin;
        Location = location;
        BindingSet = bindingSet;
        BindingSlot = bindingSlot;
    }

    public ShaderGlobal WithLocation(int? location)
        => location == Location ? this
            : new ShaderGlobal(Name, Type, GlobalKind, Builtin, location, BindingSet, BindingSlot, Diagnostics);

    public ShaderGlobal WithBinding(int? bindingSet, int? bindingSlot)
        => bindingSet == BindingSet && bindingSlot == BindingSlot ? this
            : new ShaderGlobal(Name, Type, GlobalKind, Builtin, Location, bindingSet, bindingSlot, Diagnostics);

    public override ShaderGlobal WithDiagnostics(ImmutableList<ShaderDiagnostic> diagnostics)
        => diagnostics == Diagnostics ? this
            : new ShaderGlobal(Name, Type, GlobalKind, Builtin, Location, BindingSet, BindingSlot, diagnostics);

    public override int ChildCount => 0;
    public override ShaderElement? GetChild(int index) => throw new ArgumentOutOfRangeException(nameof(index));
    public override ShaderGlobal RewriteChildren(ShaderRewriter rewriter) => this;
}
