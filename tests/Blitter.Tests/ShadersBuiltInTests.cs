namespace Blitter.Tests;

public class ShadersBuiltInTests
{
    [Fact]
    public void LitColor_FragmentShader_DeclaresPointLightStorageBuffer()
    {
        // Sanity check: the per-pixel LitColor fragment shader is the
        // first built-in to use a storage buffer (for point lights).
        // If SPIR-V reflection ever stops counting StructuredBuffer<T>
        // as a storage buffer, the GpuRenderer will silently fail to
        // bind anything, the shader will read garbage, and lit scenes
        // will look wrong without an obvious cause -- catch it here.
        var f = Shaders.LitColor.Fragment.GetResources();

        Assert.Equal(1u, f.NumStorageBuffers);
        Assert.Equal(0u, f.NumSamplers);
        Assert.Equal(0u, f.NumStorageTextures);
        // Four cbuffers: ambient, dir light direction, dir light color, point light count.
        Assert.Equal(4u, f.NumUniformBuffers);
    }

    [Fact]
    public void LitColor_VertexShader_HasNoTexturesOrStorage()
    {
        var v = Shaders.LitColor.Vertex.GetResources();

        Assert.Equal(0u, v.NumStorageBuffers);
        Assert.Equal(0u, v.NumSamplers);
        Assert.Equal(0u, v.NumStorageTextures);
        // Two cbuffers: model + view-projection.
        Assert.Equal(2u, v.NumUniformBuffers);
    }

    [Fact]
    public void LitTexture_FragmentShader_DeclaresSamplerAndStorageBuffer()
    {
        // Lit + textured shader: same lighting cbuffers as LitColor,
        // plus a Texture2D + sampler at slot 0 and the point light
        // storage buffer (which now lives at slot 1 because the sampled
        // texture takes slot 0). If reflection ever miscounts these,
        // the renderer will fail to bind something and the resulting
        // visual breakage won't be obvious -- catch it here.
        var f = Shaders.LitTexture.Fragment.GetResources();

        Assert.Equal(1u, f.NumSamplers);
        Assert.Equal(1u, f.NumStorageBuffers);
        Assert.Equal(0u, f.NumStorageTextures);
        Assert.Equal(4u, f.NumUniformBuffers);
    }

    [Fact]
    public void LitTexture_VertexShader_HasNoTexturesOrStorage()
    {
        var v = Shaders.LitTexture.Vertex.GetResources();

        Assert.Equal(0u, v.NumStorageBuffers);
        Assert.Equal(0u, v.NumSamplers);
        Assert.Equal(0u, v.NumStorageTextures);
        Assert.Equal(2u, v.NumUniformBuffers);
    }

    [Fact]
    public void LitTextureInstanced_VertexShader_HasNoTexturesOrStorage()
    {
        // Touching .GetResources() forces shadercross to compile the
        // HLSL and reflect on the SPIR-V; a malformed shader would
        // throw here, making this a cheap compile-smoke test for the
        // instanced lit-textured vertex stage.
        var v = Shaders.LitTextureInstanced.Vertex.GetResources();

        Assert.Equal(0u, v.NumStorageBuffers);
        Assert.Equal(0u, v.NumSamplers);
        Assert.Equal(0u, v.NumStorageTextures);
        // Same two cbuffers as the non-instanced LitTexture vertex
        // stage (Model + ViewProjection); Model is unused but
        // declared so we can reuse the LitArgs layout exactly.
        Assert.Equal(2u, v.NumUniformBuffers);
    }

    [Fact]
    public void LitTextureInstanced_FragmentShader_MatchesNonInstancedLitTexture()
    {
        // The instanced shader reuses the LitTexture fragment stage,
        // so its resource counts must match the non-instanced version
        // exactly (same texture binding, same lighting cbuffers, same
        // point-light storage buffer slot).
        var f = Shaders.LitTextureInstanced.Fragment.GetResources();

        Assert.Equal(1u, f.NumSamplers);
        Assert.Equal(1u, f.NumStorageBuffers);
        Assert.Equal(0u, f.NumStorageTextures);
        Assert.Equal(4u, f.NumUniformBuffers);
    }
}
