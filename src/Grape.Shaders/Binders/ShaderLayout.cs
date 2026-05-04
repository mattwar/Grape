namespace Grape.Shaders;

/// <summary>
/// Assigns <see cref="ShaderGlobal.Location"/> and
/// <see cref="ShaderGlobal.BindingSet"/> / <see cref="ShaderGlobal.BindingSlot"/>
/// values to any global that doesn't already have one. User-specified values
/// are preserved.
///
/// <para>Layout rules (the contract callers can rely on):</para>
/// <list type="bullet">
/// <item>Vertex inputs and fragment outputs get per-stage location counters from 0.</item>
/// <item>Vertex StageOutput and fragment StageInput share a name-keyed location
/// namespace, so a varying matches across stages by name.</item>
/// <item>Uniforms, textures and samplers share a name-keyed binding namespace in
/// <c>set = 0</c>: the same resource name in any stage gets the same <c>(set, slot)</c>.</item>
/// <item>PushConstant and Builtin globals get no layout assignment.</item>
/// </list>
/// </summary>
public static class ShaderLayout
{
    public static ShaderSet AssignLayout(ShaderSet shaders)
    {
        var stages = EnumerateStages(shaders).ToArray();

        // ---- Cross-stage maps (varyings + resources) ----
        var varyingLoc      = new Dictionary<string, int>();
        var resourceBinding = new Dictionary<string, (int Set, int Slot)>();

        // Seed with any user-specified values, also tracking next-free counters.
        int nextVarying = 0;
        int nextResourceSlot = 0;

        foreach (var stage in stages)
        {
            foreach (var g in stage.Globals)
            {
                if (IsVarying(stage.Stage, g) && g.Location is int vloc)
                {
                    varyingLoc[g.Name] = vloc;
                    if (vloc >= nextVarying) nextVarying = vloc + 1;
                }
                if (IsResource(g.GlobalKind) && g.BindingSlot is int slot)
                {
                    resourceBinding[g.Name] = (g.BindingSet ?? 0, slot);
                    if (slot >= nextResourceSlot) nextResourceSlot = slot + 1;
                }
            }
        }

        // Assign missing varyings: vertex outs first (so fragment ins inherit).
        foreach (var stage in stages)
        {
            if (stage.Stage != ShaderStageKind.Vertex) continue;
            foreach (var g in stage.Globals)
            {
                if (g.GlobalKind != ShaderGlobalKind.StageOutput) continue;
                if (g.Builtin != ShaderBuiltin.None) continue;
                if (g.Location is not null) continue;
                if (varyingLoc.ContainsKey(g.Name)) continue;
                varyingLoc[g.Name] = nextVarying++;
            }
        }
        foreach (var stage in stages)
        {
            if (stage.Stage != ShaderStageKind.Fragment) continue;
            foreach (var g in stage.Globals)
            {
                if (g.GlobalKind != ShaderGlobalKind.StageInput) continue;
                if (g.Builtin != ShaderBuiltin.None) continue;
                if (g.Location is not null) continue;
                if (varyingLoc.ContainsKey(g.Name)) continue;
                varyingLoc[g.Name] = nextVarying++;
            }
        }

        // Assign missing resource bindings.
        foreach (var stage in stages)
        {
            foreach (var g in stage.Globals)
            {
                if (!IsResource(g.GlobalKind)) continue;
                if (g.BindingSlot is not null) continue;
                if (resourceBinding.ContainsKey(g.Name)) continue;
                resourceBinding[g.Name] = (0, nextResourceSlot++);
            }
        }

        // ---- Per-stage maps (vertex inputs, fragment outputs) ----
        // These are independent across stages and not name-shared.
        var perStage = new Dictionary<ShaderStageKind, (int VertexInputNext, int FragmentOutputNext)>();
        foreach (var stage in stages)
        {
            int vi = 0, fo = 0;
            foreach (var g in stage.Globals)
            {
                if (g.Builtin != ShaderBuiltin.None) continue;
                if (g.Location is not int loc) continue;
                if (stage.Stage == ShaderStageKind.Vertex && g.GlobalKind == ShaderGlobalKind.VertexInput)
                    vi = Math.Max(vi, loc + 1);
                if (stage.Stage == ShaderStageKind.Fragment && g.GlobalKind == ShaderGlobalKind.StageOutput)
                    fo = Math.Max(fo, loc + 1);
            }
            perStage[stage.Stage] = (vi, fo);
        }

        // ---- Apply ----
        return shaders.WithStages(
            shaders.Vertex   is null ? null : ApplyStage(shaders.Vertex,   varyingLoc, resourceBinding, perStage),
            shaders.Fragment is null ? null : ApplyStage(shaders.Fragment, varyingLoc, resourceBinding, perStage),
            shaders.Compute  is null ? null : ApplyStage(shaders.Compute,  varyingLoc, resourceBinding, perStage));
    }

    private static ShaderStage ApplyStage(
        ShaderStage stage,
        Dictionary<string, int> varyingLoc,
        Dictionary<string, (int Set, int Slot)> resourceBinding,
        Dictionary<ShaderStageKind, (int VertexInputNext, int FragmentOutputNext)> perStage)
    {
        var counters = perStage[stage.Stage];

        var builder = ImmutableArray.CreateBuilder<ShaderGlobal>(stage.Globals.Length);
        bool changed = false;
        foreach (var g in stage.Globals)
        {
            var ng = g;

            if (g.Builtin != ShaderBuiltin.None || g.GlobalKind == ShaderGlobalKind.PushConstant)
            {
                builder.Add(ng);
                continue;
            }

            // Locations
            if (g.Location is null)
            {
                int? assigned = (stage.Stage, g.GlobalKind) switch
                {
                    (ShaderStageKind.Vertex,   ShaderGlobalKind.VertexInput)  => counters.VertexInputNext++,
                    (ShaderStageKind.Fragment, ShaderGlobalKind.StageOutput) => counters.FragmentOutputNext++,
                    (_, ShaderGlobalKind.StageOutput) when stage.Stage == ShaderStageKind.Vertex
                        => varyingLoc.TryGetValue(g.Name, out var v) ? v : null,
                    (_, ShaderGlobalKind.StageInput) when stage.Stage == ShaderStageKind.Fragment
                        => varyingLoc.TryGetValue(g.Name, out var v) ? v : null,
                    _ => null,
                };
                if (assigned is int a)
                {
                    ng = ng.WithLocation(a);
                }
            }

            // Bindings
            if (IsResource(g.GlobalKind) && g.BindingSlot is null
                && resourceBinding.TryGetValue(g.Name, out var b))
            {
                ng = ng.WithBinding(b.Set, b.Slot);
            }

            if (!ReferenceEquals(ng, g)) changed = true;
            builder.Add(ng);
        }

        if (!changed) return stage;

        // Rebuild references inside the stage body and functions so they point at
        // the new ShaderGlobal instances (preserving the post-bind invariant that
        // GlobalReferenceExpression.Global is the actual ShaderGlobal in scope).
        var newGlobals = builder.MoveToImmutable();
        var rebinder = new GlobalReferenceRewriter(newGlobals);
        var newFunctions = rebinder.Rewrite(stage.Functions);
        var newEntryBody = (ShaderExpression)rebinder.Rewrite(stage.EntryBody)!;
        return stage.WithChildren(newGlobals, newFunctions, newEntryBody);
    }

    /// <summary>Rewrites every <see cref="GlobalReferenceExpression"/> whose name appears in the new globals list.</summary>
    private sealed class GlobalReferenceRewriter(ImmutableArray<ShaderGlobal> newGlobals) : ShaderRewriter
    {
        private readonly Dictionary<string, ShaderGlobal> _byName = BuildIndex(newGlobals);

        private static Dictionary<string, ShaderGlobal> BuildIndex(ImmutableArray<ShaderGlobal> globals)
        {
            var d = new Dictionary<string, ShaderGlobal>(globals.Length);
            foreach (var g in globals) d[g.Name] = g;
            return d;
        }

        protected override ShaderElement Rewrite(ShaderElement current, ShaderElement original)
        {
            if (current is GlobalReferenceExpression gref
                && _byName.TryGetValue(gref.Name, out var g)
                && !ReferenceEquals(gref.Global, g))
            {
                return gref.WithGlobal(g);
            }
            return current;
        }
    }

    private static IEnumerable<ShaderStage> EnumerateStages(ShaderSet shaders)
    {
        if (shaders.Vertex   is { } v) yield return v;
        if (shaders.Fragment is { } f) yield return f;
        if (shaders.Compute  is { } c) yield return c;
    }

    private static bool IsVarying(ShaderStageKind stage, ShaderGlobal g) =>
        (stage == ShaderStageKind.Vertex   && g.GlobalKind == ShaderGlobalKind.StageOutput) ||
        (stage == ShaderStageKind.Fragment && g.GlobalKind == ShaderGlobalKind.StageInput);

    private static bool IsResource(ShaderGlobalKind kind) =>
        kind is ShaderGlobalKind.Uniform or ShaderGlobalKind.Texture or ShaderGlobalKind.Sampler;
}
