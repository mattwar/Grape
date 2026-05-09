using System.Buffers.Binary;
using Blitter.Shaders;

namespace Blitter.Tests;

public class SpirvReflectionTests
{
    [Fact]
    public void GetShaderInfo_Throws_WhenTooShort()
    {
        var ex = Assert.Throws<ArgumentException>(
            () => SpirvReflection.GetShaderInfo(new byte[4]));
        Assert.Contains("too short", ex.Message);
    }

    [Fact]
    public void GetShaderInfo_Throws_WhenLengthNotMultipleOf4()
    {
        var ex = Assert.Throws<ArgumentException>(
            () => SpirvReflection.GetShaderInfo(new byte[21]));
        Assert.Contains("multiple of 4", ex.Message);
    }

    [Fact]
    public void GetShaderInfo_Throws_OnWrongMagic()
    {
        // 5 words of zeros — long enough to pass header-length check, wrong magic.
        var ex = Assert.Throws<ArgumentException>(
            () => SpirvReflection.GetShaderInfo(new byte[20]));
        Assert.Contains("SPIR-V", ex.Message);
    }

    [Fact]
    public void GetShaderInfo_ReadsVertexEntryPoint()
    {
        var bytes = BuildMinimalModule(executionModel: 0u, entryName: "main");
        var info = SpirvReflection.GetShaderInfo(bytes);

        Assert.Equal(ShaderKind.Vertex, info.Stage);
        Assert.Equal("main", info.Entrypoint);
        Assert.Equal(0u, info.Resources.NumSamplers);
        Assert.Equal(0u, info.Resources.NumUniformBuffers);
        Assert.Equal(0u, info.Resources.NumStorageTextures);
        Assert.Equal(0u, info.Resources.NumStorageBuffers);
    }

    [Fact]
    public void GetShaderInfo_ReadsFragmentEntryPoint()
    {
        var bytes = BuildMinimalModule(executionModel: 4u, entryName: "psMain");
        var info = SpirvReflection.GetShaderInfo(bytes);

        Assert.Equal(ShaderKind.Fragment, info.Stage);
        Assert.Equal("psMain", info.Entrypoint);
    }

    [Fact]
    public void GetShaderInfo_Throws_OnUnsupportedExecutionModel()
    {
        // 5 = GLCompute — not supported.
        var bytes = BuildMinimalModule(executionModel: 5u, entryName: "main");
        var ex = Assert.Throws<ArgumentException>(
            () => SpirvReflection.GetShaderInfo(bytes));
        Assert.Contains("execution model", ex.Message);
    }

    [Fact]
    public void GetShaderInfo_Throws_WhenNoEntryPoints()
    {
        // Header only; no OpEntryPoint.
        var words = new uint[] { 0x07230203u, 0x00010000u, 0u, 1u, 0u };
        var ex = Assert.Throws<ArgumentException>(
            () => SpirvReflection.GetShaderInfo(WordsToBytes(words)));
        Assert.Contains("no entry points", ex.Message);
    }

    /// <summary>
    /// Builds a tiny but well-formed SPIR-V module: header + a single OpEntryPoint.
    /// Resource-related opcodes are omitted, so all counts come back as zero.
    /// </summary>
    private static byte[] BuildMinimalModule(uint executionModel, string entryName)
    {
        var words = new List<uint>
        {
            // Header: magic, version 1.0, generator, bound, schema.
            0x07230203u, 0x00010000u, 0u, 1u, 0u,
        };

        // Pack the entry-point name: UTF-8, null-terminated, padded to 4-byte words.
        var nameBytes = System.Text.Encoding.UTF8.GetBytes(entryName);
        int nameWordCount = nameBytes.Length / 4 + 1; // always at least one trailing null word
        var nameWords = new uint[nameWordCount];
        for (int i = 0; i < nameBytes.Length; i++)
        {
            nameWords[i / 4] |= (uint)nameBytes[i] << (8 * (i % 4));
        }

        // OpEntryPoint = opcode 15. Word count = 1 (op header) + 1 (model) + 1 (fn id) + N (name).
        int wordCount = 3 + nameWordCount;
        words.Add(((uint)wordCount << 16) | 15u);
        words.Add(executionModel);
        words.Add(1u); // function id
        words.AddRange(nameWords);

        return WordsToBytes(words);
    }

    private static byte[] WordsToBytes(IReadOnlyList<uint> words)
    {
        var bytes = new byte[words.Count * 4];
        for (int i = 0; i < words.Count; i++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(i * 4, 4), words[i]);
        }
        return bytes;
    }
}
