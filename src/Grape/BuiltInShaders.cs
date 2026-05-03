using System.Collections.Immutable;
using static SDL3.SDL;

namespace Grape;

/// <summary>
/// Lazily creates the shader bytecode bundled with Grape as embedded resources.
/// </summary>
/// <remarks>
/// Three precompiled formats ship with the library: SPIR-V (Vulkan), DXIL (D3D12),
/// and MSL (Metal). The loader picks the first supported format reported by the
/// device.
/// </remarks>
public sealed class BuiltInShaders
{
    private readonly GpuDevice _device;

    private GpuShader? _positionColorVert;
    private GpuShader? _positionColorTransformVert;
    private GpuShader? _texturedQuadVert;
    private GpuShader? _texturedQuadWithMatrixVert;
    private GpuShader? _solidColorFrag;
    private GpuShader? _texturedQuadFrag;

    private Shader<ColorVertex3D>? _positionColor;
    private Shader<ColorVertex3D>? _positionColorTransform;
    private Shader<TextureVertex3D>? _texturedQuad;
    private Shader<TextureVertex3D>? _texturedQuadWithMatrix;

    public BuiltInShaders(GpuDevice device)
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

    /// <summary>
    /// Vertex shader: does not alter the vertex position or color. Positions
    /// must already be in normalized device coordinates (the visible cube is
    /// -1 to 1 on each axis). Useful for screen-space drawing or testing.
    /// </summary>
    public GpuShader PositionColorVert =>
        _positionColorVert ??= CreateShader("PositionColor.vert");

    /// <summary>
    /// Vertex shader: transforms the vertex position via a 4x4 matrix (in slot 0)
    /// and does not alter the color. Use this for normal 3D drawing where you
    /// supply a model-view-projection matrix.
    /// </summary>
    public GpuShader PositionColorTransformVert =>
        _positionColorTransformVert ??= CreateShader("PositionColorTransform.vert", numUniformBuffers: 1);

    /// <summary>
    /// Vertex shader: does not alter the vertex position or texture coordinate.
    /// Positions must already be in normalized device coordinates (the visible
    /// cube is -1 to 1 on each axis). Useful for full-screen quads or
    /// screen-space drawing.
    /// </summary>
    public GpuShader TexturedQuadVert =>
        _texturedQuadVert ??= CreateShader("TexturedQuad.vert");

    /// <summary>
    /// Vertex shader: transforms the vertex position via a 4x4 matrix (in slot 0)
    /// and does not alter the texture coordinate. Use this for normal 3D drawing
    /// of textured meshes where you supply a model-view-projection matrix.
    /// </summary>
    public GpuShader TexturedQuadWithMatrixVert =>
        _texturedQuadWithMatrixVert ??= CreateShader("TexturedQuadWithMatrix.vert", numUniformBuffers: 1);

    /// <summary>
    /// Fragment shader: outputs the per-vertex color as the pixel color. Pair
    /// with <see cref="PositionColorVert"/> or
    /// <see cref="PositionColorTransformVert"/>.
    /// </summary>
    public GpuShader SolidColorFrag =>
        _solidColorFrag ??= CreateShader("SolidColor.frag");

    /// <summary>
    /// Fragment shader: looks up a pixel from a bound texture using the
    /// per-vertex texture coordinate. Pair with <see cref="TexturedQuadVert"/>
    /// or <see cref="TexturedQuadWithMatrixVert"/>.
    /// </summary>
    public GpuShader TexturedQuadFrag =>
        _texturedQuadFrag ??= CreateShader("TexturedQuad.frag", numSamplers: 1);

    private GpuShader CreateShader(
        string shaderName,
        uint numSamplers = 0,
        uint numUniformBuffers = 0,
        uint numStorageTextures = 0,
        uint numStorageBuffers = 0)
    {
        var stage = shaderName.EndsWith(".vert", StringComparison.Ordinal)
            ? GPUShaderStage.Vertex
            : GPUShaderStage.Fragment;

        var (format, folder, fileExt, entryPoint) = SelectFormat(_device.ShaderFormat);
        var resourcePath = $"{folder}.{shaderName}.{fileExt}";
        var code = LoadEmbedded(resourcePath);

        return _device.CreateShader(new GpuShaderCreateInfo
        {
            Code = code,
            Entrypoint = entryPoint,
            Format = format,
            Stage = stage,
            NumSamplers = numSamplers,
            NumUniformBuffers = numUniformBuffers,
            NumStorageTextures = numStorageTextures,
            NumStorageBuffers = numStorageBuffers,
        });
    }

    private static (GPUShaderFormat format, string folder, string fileExt, string entryPoint) SelectFormat(GPUShaderFormat supported)
    {
        if ((supported & GPUShaderFormat.SPIRV) != 0)
            return (GPUShaderFormat.SPIRV, "SPIRV", "spv", "main");
        if ((supported & GPUShaderFormat.DXIL) != 0)
            return (GPUShaderFormat.DXIL, "DXIL", "dxil", "main");
        if ((supported & GPUShaderFormat.MSL) != 0)
            return (GPUShaderFormat.MSL, "MSL", "msl", "main0");

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
