namespace Grape.Shaders;

/// <summary>
/// Texture sample. Distinct from <see cref="CallExpression"/> because operand
/// shape is fixed (texture + sampler + uv [+ optional lod]).
/// </summary>
public sealed class SampleExpression : ShaderExpression
{
    public ShaderExpression Texture { get; }
    public ShaderExpression Sampler { get; }
    public ShaderExpression Coord   { get; }
    /// <summary>Null = implicit LOD via fragment-stage derivatives.</summary>
    public ShaderExpression? Lod    { get; }

    public SampleExpression(
        ShaderExpression texture,
        ShaderExpression sampler,
        ShaderExpression coord,
        ShaderExpression? lod = null)
        : this(texture, sampler, coord, lod, null, null) { }

    private SampleExpression(
        ShaderExpression texture,
        ShaderExpression sampler,
        ShaderExpression coord,
        ShaderExpression? lod,
        ShaderType? resultType,
        ImmutableList<ShaderDiagnostic>? diagnostics)
        : base(State(texture) | State(sampler) | State(coord) | State(lod), resultType, diagnostics)
    {
        Texture = texture;
        Sampler = sampler;
        Coord = coord;
        Lod = lod;
    }

    public override SampleExpression WithResultType(ShaderType? resultType)
        => ReferenceEquals(resultType, ResultType) ? this
            : new SampleExpression(Texture, Sampler, Coord, Lod, resultType, Diagnostics);

    public override SampleExpression WithDiagnostics(ImmutableList<ShaderDiagnostic> diagnostics)
        => diagnostics == Diagnostics ? this
            : new SampleExpression(Texture, Sampler, Coord, Lod, ResultType, diagnostics);

    public SampleExpression WithOperands(
        ShaderExpression texture,
        ShaderExpression sampler,
        ShaderExpression coord,
        ShaderExpression? lod)
        => ReferenceEquals(texture, Texture)
            && ReferenceEquals(sampler, Sampler)
            && ReferenceEquals(coord, Coord)
            && ReferenceEquals(lod, Lod)
                ? this
                : new SampleExpression(texture, sampler, coord, lod, ResultType, Diagnostics);

    public override int ChildCount => 4;
    public override ShaderElement? GetChild(int index) => index switch
    {
        0 => Texture,
        1 => Sampler,
        2 => Coord,
        3 => Lod,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };

    public override SampleExpression RewriteChildren(ShaderRewriter rewriter)
    {
        var t = (ShaderExpression)rewriter.Rewrite(Texture)!;
        var s = (ShaderExpression)rewriter.Rewrite(Sampler)!;
        var c = (ShaderExpression)rewriter.Rewrite(Coord)!;
        var l = (ShaderExpression?)rewriter.Rewrite(Lod);
        return WithOperands(t, s, c, l);
    }
}
