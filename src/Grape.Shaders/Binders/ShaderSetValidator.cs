namespace Grape.Shaders;

/// <summary>
/// Validates the cross-stage interface of a <see cref="ShaderSet"/>:
/// varyings line up between stages and shared resources agree on kind and type.
///
/// <para>Diagnostics:</para>
/// <list type="bullet">
/// <item><b>SH0200</b> fragment input has no matching vertex output.</item>
/// <item><b>SH0201</b> matched varying has mismatched types.</item>
/// <item><b>SH0202</b> cross-stage resource has conflicting kinds.</item>
/// <item><b>SH0203</b> cross-stage resource has conflicting types.</item>
/// </list>
/// </summary>
public static class ShaderSetValidator
{
    public static BindResult<ShaderSet> Validate(ShaderSet set)
    {
        var diags = ImmutableList.CreateBuilder<ShaderDiagnostic>();

        ValidateVaryings(set, diags);
        ValidateCrossStageResources(set, diags);

        if (diags.Count == 0)
            return new BindResult<ShaderSet>(set, set.GetContainedDiagnostics());

        var attached = (ShaderSet)set.WithDiagnostics(set.Diagnostics.AddRange(diags));
        return new BindResult<ShaderSet>(attached, attached.GetContainedDiagnostics());
    }

    private static void ValidateVaryings(ShaderSet set, ImmutableList<ShaderDiagnostic>.Builder diags)
    {
        var vs = set.Vertex;
        var fs = set.Fragment;
        if (vs is null || fs is null) return;

        var vsOuts = new Dictionary<string, ShaderGlobal>(StringComparer.Ordinal);
        foreach (var g in vs.Globals)
            if (g.GlobalKind == ShaderGlobalKind.StageOutput && g.Builtin == ShaderBuiltin.None)
                vsOuts[g.Name] = g;

        foreach (var g in fs.Globals)
        {
            if (g.GlobalKind != ShaderGlobalKind.StageInput) continue;
            if (g.Builtin != ShaderBuiltin.None) continue;

            if (!vsOuts.TryGetValue(g.Name, out var match))
            {
                diags.Add(Error("SH0200",
                    $"Fragment input '{g.Name}' has no matching vertex output."));
                continue;
            }

            if (!ReferenceEquals(g.Type, match.Type))
            {
                diags.Add(Error("SH0201",
                    $"Varying '{g.Name}' has mismatched types: vertex output is '{match.Type}', fragment input is '{g.Type}'."));
            }
        }
    }

    private static void ValidateCrossStageResources(ShaderSet set, ImmutableList<ShaderDiagnostic>.Builder diags)
    {
        var firstSeen = new Dictionary<string, ShaderGlobal>(StringComparer.Ordinal);

        foreach (var stage in EnumerateStages(set))
        {
            foreach (var g in stage.Globals)
            {
                if (!IsCrossStageResource(g.GlobalKind)) continue;

                if (!firstSeen.TryGetValue(g.Name, out var first))
                {
                    firstSeen[g.Name] = g;
                    continue;
                }

                if (g.GlobalKind != first.GlobalKind)
                {
                    diags.Add(Error("SH0202",
                        $"Resource '{g.Name}' has conflicting kinds across stages: '{first.GlobalKind}' vs '{g.GlobalKind}'."));
                }
                else if (!ReferenceEquals(g.Type, first.Type))
                {
                    diags.Add(Error("SH0203",
                        $"Resource '{g.Name}' has conflicting types across stages: '{first.Type}' vs '{g.Type}'."));
                }
            }
        }
    }

    private static bool IsCrossStageResource(ShaderGlobalKind kind) =>
        kind is ShaderGlobalKind.Uniform
             or ShaderGlobalKind.Texture
             or ShaderGlobalKind.Sampler
             or ShaderGlobalKind.PushConstant;

    private static IEnumerable<ShaderStage> EnumerateStages(ShaderSet set)
    {
        if (set.Vertex   is { } v) yield return v;
        if (set.Fragment is { } f) yield return f;
        if (set.Compute  is { } c) yield return c;
    }

    private static ShaderDiagnostic Error(string code, string message)
        => new(ShaderDiagnosticSeverity.Error, code, message);
}
