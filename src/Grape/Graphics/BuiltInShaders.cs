using System.Collections.Immutable;
using static SDL3.SDL;

namespace Grape;

/// <summary>
/// Lazily creates the shader bytecode bundled with Grape as embedded resources.
/// </summary>
/// <remarks>
/// Three precompiled formats ship with the library: SPIR-V (Vulkan), DXIL (D3D12),
/// and MSL (Metal). The loader picks the first supported format reported by the
/// device. Returned shaders hold CPU-side bytecode only; the renderer uploads
/// them to the GPU on first use.
/// </remarks>
public sealed class BuiltInShaders
{
    private readonly GpuDevice _device;

    private StageShader? _positionColorVert;
    private StageShader? _positionColorTransformVert;
    private StageShader? _texturedQuadVert;
    private StageShader? _texturedQuadWithMatrixVert;
    private StageShader? _solidColorFrag;
    private StageShader? _texturedQuadFrag;

    private Shader<ColorVertex3D>? _positionColor;
    private Shader<ColorVertex3D>? _positionColorTransform;
    private Shader<TextureVertex3D>? _texturedQuad;
    private Shader<TextureVertex3D>? _texturedQuadWithMatrix;

    internal BuiltInShaders(GpuDevice device)
    {
        _device = device;
    }

    /// <summary>
    /// Draws each vertex at its position with its baked color, with no
    /// transformation. Positions must already be in normalized device
    /// coordinates (the visible cube is -1 to 1 on each axis). Useful for
    /// screen-space drawing or testing.
    /// </summary>
    public Shader<ColorVertex3D> PositionColor =>
        _positionColor ??= new Shader<ColorVertex3D>(
            PositionColorVert,
            SolidColorFrag,
            ColoredMesh.VertexLayout);

    /// <summary>
    /// Draws each vertex at its position with its color, transforming
    /// the position by a 4x4 model-view-projection matrix.
    /// </summary>
    public Shader<ColorVertex3D> PositionColorTransform =>
        _positionColorTransform ??= new Shader<ColorVertex3D>(
            PositionColorTransformVert,
            SolidColorFrag,
            ColoredMesh.VertexLayout,
            requiresTransform: true);

    /// <summary>
    /// Draws each vertex at its position, sampling the bound texture using the
    /// vertex texture coordinate, with no transformation. Positions must
    /// already be in normalized device coordinates.
    /// </summary>
    public Shader<TextureVertex3D> TexturedQuad =>
        _texturedQuad ??= new Shader<TextureVertex3D>(
            TexturedQuadVert,
            TexturedQuadFrag,
            TexturedMesh.VertexLayout);

    /// <summary>
    /// Draws each vertex at its position transformed by a 4x4
    /// model-view-projection matrix, sampling the bound texture using the
    /// vertex texture coordinate.
    /// </summary>
    public Shader<TextureVertex3D> TexturedQuadWithMatrix =>
        _texturedQuadWithMatrix ??= new Shader<TextureVertex3D>(
            TexturedQuadWithMatrixVert,
            TexturedQuadFrag,
            TexturedMesh.VertexLayout,
            requiresTransform: true);

    private StageShader PositionColorVert =>
        _positionColorVert ??= LoadStage("PositionColor.vert", StageShaderKind.Vertex);

    private StageShader PositionColorTransformVert =>
        _positionColorTransformVert ??= LoadStage(
            "PositionColorTransform.vert", StageShaderKind.Vertex,
            new ShaderResourceCounts(NumUniformBuffers: 1));

    private StageShader TexturedQuadVert =>
        _texturedQuadVert ??= LoadStage("TexturedQuad.vert", StageShaderKind.Vertex);

    private StageShader TexturedQuadWithMatrixVert =>
        _texturedQuadWithMatrixVert ??= LoadStage(
            "TexturedQuadWithMatrix.vert", StageShaderKind.Vertex,
            new ShaderResourceCounts(NumUniformBuffers: 1));

    private StageShader SolidColorFrag =>
        _solidColorFrag ??= LoadStage("SolidColor.frag", StageShaderKind.Fragment);

    private StageShader TexturedQuadFrag =>
        _texturedQuadFrag ??= LoadStage(
            "TexturedQuad.frag", StageShaderKind.Fragment,
            new ShaderResourceCounts(NumSamplers: 1));

    private StageShader LoadStage(
        string shaderName,
        StageShaderKind kind,
        ShaderResourceCounts resources = default)
    {
        var (format, folder, fileExt, entryPoint) = SelectFormat(_device.ShaderFormat);
        var resourcePath = $"{folder}.{shaderName}.{fileExt}";
        var code = LoadEmbedded(resourcePath);
        return new StageShader(kind, format, code, resources, entryPoint);
    }

    private static (ShaderFormat format, string folder, string fileExt, string entryPoint) SelectFormat(GPUShaderFormat supported)
    {
        if ((supported & GPUShaderFormat.SPIRV) != 0)
            return (ShaderFormat.Spirv, "SPIRV", "spv", "main");
        if ((supported & GPUShaderFormat.DXIL) != 0)
            return (ShaderFormat.Dxil, "DXIL", "dxil", "main");
        if ((supported & GPUShaderFormat.MSL) != 0)
            return (ShaderFormat.Msl, "MSL", "msl", "main0");

        throw new NotSupportedException(
            $"GpuDevice reports no supported shader format compatible with bundled shaders. Reported: {supported}");
    }

    private static ImmutableArray<byte> LoadEmbedded(string resourcePath)
    {
        var asm = typeof(BuiltInShaders).Assembly;
        var fullName = $"Grape.Shaders.{resourcePath}";

        using var stream = asm.GetManifestResourceStream(fullName)
            ?? throw new InvalidOperationException(
                $"Embedded shader resource not found: {fullName}. Available resources: " +
                string.Join(", ", asm.GetManifestResourceNames()));

        var buffer = new byte[stream.Length];
        var read = 0;
        while (read < buffer.Length)
        {
            var n = stream.Read(buffer, read, buffer.Length - read);
            if (n == 0) break;
            read += n;
        }
        return ImmutableArray.Create(buffer);
    }
}
