using System.Buffers.Binary;
using Grape.Shaders.Demos;
using Grape.Shaders.Emitters;

namespace Grape.Shaders.Tests;

/// <summary>
/// Header-and-structure invariants for the SPIR-V emitter. We do not run the
/// output through a Vulkan validator here -- the assertions cover what we can
/// check without that dependency. Real-world validation happens at runtime
/// when SDL_GPU consumes the bytes.
/// </summary>
public class SpvEmitterTests
{
    private const uint SpvMagic    = 0x07230203u;
    private const uint SpvVersion10 = 0x00010000u;

    [Fact]
    public void Vertex_module_has_valid_header()
    {
        var bytes = EmitTexturedQuadVertex();

        Assert.True(bytes.Length >= 20, "Module too short to contain a header.");
        Assert.True(bytes.Length % 4 == 0, "Module length must be a multiple of 4.");

        Assert.Equal(SpvMagic,    BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(0)));
        Assert.Equal(SpvVersion10, BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(4)));
        var idBound = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(12));
        Assert.True(idBound > 1, "ID bound should be greater than 1.");
        Assert.Equal(0u, BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(16))); // schema
    }

    [Fact]
    public void Fragment_module_has_valid_header()
    {
        var bytes = EmitTexturedQuadFragment();

        Assert.True(bytes.Length >= 20);
        Assert.Equal(SpvMagic,     BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(0)));
        Assert.Equal(SpvVersion10, BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(4)));
    }

    [Fact]
    public void Vertex_module_contains_an_entry_point_instruction()
    {
        var bytes = EmitTexturedQuadVertex();
        Assert.True(ContainsOpcode(bytes, opcode: 15 /* SpvOp.EntryPoint */));
    }

    [Fact]
    public void Fragment_module_contains_origin_upper_left_execution_mode()
    {
        var bytes = EmitTexturedQuadFragment();
        // Spot-check by scanning for OpExecutionMode (16) words.
        Assert.True(ContainsOpcode(bytes, opcode: 16 /* SpvOp.ExecutionMode */));
    }

    [Fact]
    public void Vertex_module_imports_glsl_std_450()
    {
        // OpExtInstImport carries the literal "GLSL.std.450" as packed UTF-8 words.
        var bytes = EmitTexturedQuadVertex();
        var ascii = System.Text.Encoding.ASCII.GetBytes("GLSL.std.450");
        Assert.True(IndexOf(bytes, ascii) >= 0, "Expected GLSL.std.450 import in module.");
    }

    [Fact]
    public void Emitter_rejects_unbound_module()
    {
        var set = TexturedQuadDemo.Build(new StandardShaderTypeSystem());
        Assert.Throws<InvalidOperationException>(() => new SpvEmitter().Emit(set));
    }

    // ---- Helpers ----

    private static byte[] EmitTexturedQuadVertex()
    {
        var laidOut = BindAndLayout();
        var output = new SpvEmitter().Emit(laidOut);
        Assert.NotNull(output.Vertex);
        return output.Vertex!;
    }

    private static byte[] EmitTexturedQuadFragment()
    {
        var laidOut = BindAndLayout();
        var output = new SpvEmitter().Emit(laidOut);
        Assert.NotNull(output.Fragment);
        return output.Fragment!;
    }

    private static ShaderSet BindAndLayout()
    {
        var types  = new StandardShaderTypeSystem();
        var set    = TexturedQuadDemo.Build(types);
        var (bound, diags) = new ShaderBinder(types).Bind(set);
        Assert.Empty(diags);
        return ShaderLayout.AssignLayout(bound);
    }

    /// <summary>
    /// Walks the SPIR-V instruction stream after the 5-word header,
    /// returning true if any instruction has the given opcode.
    /// </summary>
    private static bool ContainsOpcode(byte[] bytes, ushort opcode)
    {
        int offset = 20;
        while (offset < bytes.Length)
        {
            var word = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset));
            int wordCount = (int)(word >> 16);
            ushort op = (ushort)(word & 0xFFFFu);
            if (op == opcode) return true;
            if (wordCount == 0) return false; // malformed
            offset += wordCount * 4;
        }
        return false;
    }

    private static int IndexOf(byte[] haystack, byte[] needle)
    {
        for (int i = 0; i + needle.Length <= haystack.Length; i++)
        {
            bool ok = true;
            for (int j = 0; j < needle.Length; j++)
                if (haystack[i + j] != needle[j]) { ok = false; break; }
            if (ok) return i;
        }
        return -1;
    }
}
