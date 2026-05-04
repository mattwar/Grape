using Grape.Shaders.Emitters.Spv;

namespace Grape.Shaders.Emitters;

/// <summary>
/// Per-stage SPIR-V bytecode produced by <see cref="SpvEmitter.Emit(ShaderSet)"/>.
/// </summary>
public sealed class SpvEmitOutput
{
    public byte[]? Vertex   { get; init; }
    public byte[]? Fragment { get; init; }
    public byte[]? Compute  { get; init; }
}

/// <summary>
/// Lowers a fully-bound <see cref="ShaderSet"/> to SPIR-V 1.0 binary modules (one per stage).
/// Targets the Vulkan environment with the GLSL.std.450 extended instruction set.
/// </summary>
public sealed class SpvEmitter
{
    public SpvEmitOutput Emit(ShaderSet set)
    {
        if (set.IsUnbound)
            throw new InvalidOperationException("ShaderSet is not fully bound; run ShaderBinder before emitting.");

        return new SpvEmitOutput
        {
            Vertex   = set.Vertex   is null ? null : new SpvStageEmitter().Emit(set.Vertex),
            Fragment = set.Fragment is null ? null : new SpvStageEmitter().Emit(set.Fragment),
            Compute  = set.Compute  is null ? null : new SpvStageEmitter().Emit(set.Compute),
        };
    }

    /// <summary>
    /// Lowers a single bound stage to SPIR-V bytes.
    /// </summary>
    public byte[] Emit(ShaderStage stage)
    {
        if (stage.IsUnbound)
            throw new InvalidOperationException("ShaderStage is not fully bound; run ShaderBinder before emitting.");
        return new SpvStageEmitter().Emit(stage);
    }
}
