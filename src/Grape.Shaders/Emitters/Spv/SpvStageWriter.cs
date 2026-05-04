using System.Buffers.Binary;

namespace Grape.Shaders.Emitters.Spv;

/// <summary>
/// Serializes an <see cref="SpvStageBuilder"/>'s sections to a SPIR-V binary
/// (header + canonical-order concatenation of section words).
/// </summary>
internal static class SpvStageWriter
{
    public static byte[] ToBytes(SpvStageBuilder b)
    {
        // Section order per SPIR-V 1.0 Section 2.4.
        var sections = new[]
        {
            b.Capabilities, b.Extensions, b.ExtInstImports, b.MemoryModel,
            b.EntryPoints, b.ExecutionModes, b.Debug, b.Annotations,
            b.TypesConstants, b.Functions,
        };

        int totalWords = 5; // header
        foreach (var s in sections) totalWords += s.Count;

        var bytes = new byte[totalWords * 4];
        var span = bytes.AsSpan();

        // Header.
        BinaryPrimitives.WriteUInt32LittleEndian(span[0..],  SpvHeader.Magic);
        BinaryPrimitives.WriteUInt32LittleEndian(span[4..],  SpvHeader.Version10);
        BinaryPrimitives.WriteUInt32LittleEndian(span[8..],  SpvHeader.Generator);
        BinaryPrimitives.WriteUInt32LittleEndian(span[12..], b.IdBound);
        BinaryPrimitives.WriteUInt32LittleEndian(span[16..], SpvHeader.Schema);

        int offset = 20;
        foreach (var section in sections)
        {
            for (int i = 0; i < section.Count; i++)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(span[offset..], section[i]);
                offset += 4;
            }
        }

        return bytes;
    }
}
