using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;

namespace Grape;

/// <summary>
/// Result of reflecting a SPIR-V module: the shader stage, the entry-point
/// name, and the resource counts SDL3 GPU needs to create the shader.
/// </summary>
internal sealed record SpirvShaderInfo(
    ShaderKind Stage,
    string Entrypoint,
    ShaderResourceCounts Resources);

/// <summary>
/// A small SPIR-V binary reflector. Walks a SPIR-V module to extract the
/// information SDL3 GPU needs to create a <see cref="Shader"/> from raw
/// bytes -- the stage kind, entry-point name, and resource counts -- so
/// callers don't have to track that metadata separately when loading
/// precompiled shaders.
/// </summary>
/// <remarks>
/// This is not a full SPIR-V validator; it inspects only the opcodes needed
/// for resource counting and entry-point discovery. Modules with multiple
/// entry points or unsupported execution models (compute, geometry, …) are
/// rejected with an exception.
///
/// Resource counting follows SDL3 GPU's conventions:
/// <list type="bullet">
///   <item><c>NumSamplers</c> = number of <c>OpTypeSampler</c> +
///         <c>OpTypeSampledImage</c> variables (textures bound through a
///         sampler slot; the paired <c>OpTypeImage</c> in the
///         separate-sampler shape is not double-counted).</item>
///   <item><c>NumStorageTextures</c> = number of <c>OpTypeImage</c>
///         variables with <c>Sampled=2</c> (storage images).</item>
///   <item><c>NumStorageBuffers</c> = number of variables in the
///         <c>StorageBuffer</c> storage class, plus legacy <c>BufferBlock</c>
///         decorated structs in <c>Uniform</c>.</item>
///   <item><c>NumUniformBuffers</c> = number of variables in the
///         <c>Uniform</c> storage class whose pointed struct has
///         <c>Block</c> (and not <c>BufferBlock</c>) decoration.</item>
/// </list>
/// </remarks>
internal static class SpirvReflection
{
    private const uint SpvMagic = 0x07230203u;
    private const int  HeaderWords = 5;

    /// <summary>
    /// Extracts the <see cref="SpirvShaderInfo"/> from the encoded SPIR-V module.
    /// </summary>
    public static SpirvShaderInfo GetShaderInfo(ReadOnlySpan<byte> code)
    {
        if (code.Length < HeaderWords * 4)
            throw new ArgumentException("SPIR-V module too short to contain a header.", nameof(code));
        if (code.Length % 4 != 0)
            throw new ArgumentException("SPIR-V module length must be a multiple of 4 bytes.", nameof(code));

        var magic = BinaryPrimitives.ReadUInt32LittleEndian(code);
        if (magic != SpvMagic)
            throw new ArgumentException(
                $"Not a (little-endian) SPIR-V module: expected magic 0x{SpvMagic:X8} but found 0x{magic:X8}.",
                nameof(code));

        var words = new SpvWords(code);

        // First pass: scan for type/decoration info we need to resolve OpVariable
        // categories on a single linear pass.
        var pointerInfo = new Dictionary<uint, PointerInfo>();
        var imageSampled = new Dictionary<uint, uint>();      // image-type id -> Sampled value
        var sampledImageOf = new Dictionary<uint, uint>();    // sampled-image type id -> underlying image type id
        var samplerTypeIds = new HashSet<uint>();
        var sampledImageTypeIds = new HashSet<uint>();
        var blockStructIds = new HashSet<uint>();
        var bufferBlockStructIds = new HashSet<uint>();

        ImmutableArray<EntryPoint>.Builder entryPoints = ImmutableArray.CreateBuilder<EntryPoint>();
        ImmutableArray<VariableInfo>.Builder variables = ImmutableArray.CreateBuilder<VariableInfo>();

        int wordIndex = HeaderWords;
        int totalWords = code.Length / 4;
        while (wordIndex < totalWords)
        {
            var instr = words[wordIndex];
            int opcode = (int)(instr & 0xFFFFu);
            int wordCount = (int)((instr >> 16) & 0xFFFFu);
            if (wordCount < 1 || wordIndex + wordCount > totalWords)
                throw new ArgumentException("Malformed SPIR-V instruction.", nameof(code));

            switch ((SpvOp)opcode)
            {
                case SpvOp.EntryPoint:
                {
                    // word 1 = ExecutionModel, word 2 = function id, word 3+ = name (packed UTF-8)
                    if (wordCount < 4) goto default;
                    var execModel = words[wordIndex + 1];
                    var (name, nameWordCount) = ReadString(words, wordIndex + 3, wordCount - 3);
                    var stage = ToStage(execModel);
                    if (stage is null)
                        throw new ArgumentException(
                            $"SPIR-V entry point has unsupported execution model {execModel}.",
                            nameof(code));
                    entryPoints.Add(new EntryPoint(stage.Value, name));
                    _ = nameWordCount;
                    break;
                }

                case SpvOp.Decorate:
                {
                    // word 1 = target, word 2 = decoration, word 3+ = literals
                    if (wordCount < 3) break;
                    var target = words[wordIndex + 1];
                    var decoration = (SpvDecoration)words[wordIndex + 2];
                    if (decoration == SpvDecoration.Block) blockStructIds.Add(target);
                    else if (decoration == SpvDecoration.BufferBlock) bufferBlockStructIds.Add(target);
                    break;
                }

                case SpvOp.TypePointer:
                {
                    // word 1 = result, word 2 = StorageClass, word 3 = pointee
                    if (wordCount < 4) break;
                    var resultId  = words[wordIndex + 1];
                    var storage   = (SpvStorageClass)words[wordIndex + 2];
                    var pointeeId = words[wordIndex + 3];
                    pointerInfo[resultId] = new PointerInfo(storage, pointeeId);
                    break;
                }

                case SpvOp.TypeImage:
                {
                    // word 1 = result, words 2..7 = type info, word 7 = Sampled
                    if (wordCount < 8) break;
                    var resultId = words[wordIndex + 1];
                    var sampled  = words[wordIndex + 7];
                    imageSampled[resultId] = sampled;
                    break;
                }

                case SpvOp.TypeSampler:
                {
                    if (wordCount < 2) break;
                    samplerTypeIds.Add(words[wordIndex + 1]);
                    break;
                }

                case SpvOp.TypeSampledImage:
                {
                    // word 1 = result, word 2 = image type id
                    if (wordCount < 3) break;
                    var resultId = words[wordIndex + 1];
                    var imageId  = words[wordIndex + 2];
                    sampledImageTypeIds.Add(resultId);
                    sampledImageOf[resultId] = imageId;
                    break;
                }

                case SpvOp.Variable:
                {
                    // word 1 = result type (pointer), word 2 = result id, word 3 = StorageClass
                    if (wordCount < 4) break;
                    var pointerTypeId = words[wordIndex + 1];
                    var resultId      = words[wordIndex + 2];
                    var storage       = (SpvStorageClass)words[wordIndex + 3];
                    variables.Add(new VariableInfo(resultId, pointerTypeId, storage));
                    break;
                }

                default:
                    break;
            }

            wordIndex += wordCount;
        }

        if (entryPoints.Count == 0)
            throw new ArgumentException("SPIR-V module declares no entry points.", nameof(code));
        if (entryPoints.Count > 1)
            throw new ArgumentException(
                $"SPIR-V module declares {entryPoints.Count} entry points; this loader supports modules with exactly one.",
                nameof(code));

        uint numSamplers = 0;
        uint numStorageTextures = 0;
        uint numStorageBuffers = 0;
        uint numUniformBuffers = 0;

        foreach (var v in variables)
        {
            if (!pointerInfo.TryGetValue(v.PointerTypeId, out var pi))
                continue; // not a typed variable we care about

            switch (pi.Storage)
            {
                case SpvStorageClass.UniformConstant:
                    if (samplerTypeIds.Contains(pi.PointeeId))
                    {
                        numSamplers++;
                    }
                    else if (sampledImageTypeIds.Contains(pi.PointeeId))
                    {
                        numSamplers++;
                    }
                    else if (imageSampled.TryGetValue(pi.PointeeId, out var sampled))
                    {
                        // Sampled=2 means "used as a storage image"; Sampled=1 is
                        // a regular sampled texture which is paired with a separate
                        // sampler global and counted via that sampler.
                        if (sampled == 2u) numStorageTextures++;
                    }
                    break;

                case SpvStorageClass.Uniform:
                    if (bufferBlockStructIds.Contains(pi.PointeeId))
                        numStorageBuffers++;
                    else if (blockStructIds.Contains(pi.PointeeId))
                        numUniformBuffers++;
                    break;

                case SpvStorageClass.StorageBuffer:
                    numStorageBuffers++;
                    break;

                // Input/Output/Function/PushConstant/Private etc. don't count
                // toward SDL3's resource totals.
                default:
                    break;
            }
        }

        var ep = entryPoints[0];
        return new SpirvShaderInfo(
            ep.Stage,
            ep.Name,
            new ShaderResourceCounts(
                NumSamplers: numSamplers,
                NumUniformBuffers: numUniformBuffers,
                NumStorageTextures: numStorageTextures,
                NumStorageBuffers: numStorageBuffers));
    }

    private static ShaderKind? ToStage(uint executionModel) => executionModel switch
    {
        0u => ShaderKind.Vertex,    // Vertex
        4u => ShaderKind.Fragment,  // Fragment
        _  => null,                      // TessControl/Eval, Geometry, GLCompute, etc.
    };

    private static (string Name, int WordCount) ReadString(SpvWords words, int firstWord, int maxWords)
    {
        // SPIR-V literal strings: UTF-8, null-terminated, packed 4 bytes per
        // word (low byte = first byte). At least one terminating null, then
        // padded out to a 4-byte boundary.
        var sb = new StringBuilder();
        int consumed = 0;
        for (int i = 0; i < maxWords; i++)
        {
            consumed++;
            uint w = words[firstWord + i];
            for (int b = 0; b < 4; b++)
            {
                byte ch = (byte)((w >> (8 * b)) & 0xFFu);
                if (ch == 0)
                    return (sb.ToString(), consumed);
                sb.Append((char)ch);
            }
        }
        return (sb.ToString(), consumed);
    }

    /// <summary>Indexed view over a span as little-endian SPIR-V words.</summary>
    private readonly ref struct SpvWords
    {
        private readonly ReadOnlySpan<byte> _bytes;
        public SpvWords(ReadOnlySpan<byte> bytes) { _bytes = bytes; }
        public uint this[int wordIndex] =>
            BinaryPrimitives.ReadUInt32LittleEndian(_bytes.Slice(wordIndex * 4, 4));
    }

    private readonly record struct EntryPoint(ShaderKind Stage, string Name);

    private readonly record struct VariableInfo(uint Id, uint PointerTypeId, SpvStorageClass Storage);

    private readonly record struct PointerInfo(SpvStorageClass Storage, uint PointeeId);

    /// <summary>SPIR-V opcodes the reflector inspects.</summary>
    private enum SpvOp
    {
        TypeImage        = 25,
        TypeSampler      = 26,
        TypeSampledImage = 27,
        TypePointer      = 32,
        Variable         = 59,
        Decorate         = 71,
        EntryPoint       = 15,
    }

    /// <summary>SPIR-V storage classes the reflector inspects.</summary>
    private enum SpvStorageClass : uint
    {
        UniformConstant = 0,
        Input           = 1,
        Uniform         = 2,
        Output          = 3,
        PushConstant    = 9,
        StorageBuffer   = 12,
    }

    /// <summary>SPIR-V decorations the reflector inspects.</summary>
    private enum SpvDecoration : uint
    {
        Block       = 2,
        BufferBlock = 3,
    }
}
