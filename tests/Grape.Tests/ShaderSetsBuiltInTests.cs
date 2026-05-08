namespace Grape.Tests;

public class ShaderSetsBuiltInTests
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
        var f = ShaderSets.LitColor.Fragment.GetResources();

        Assert.Equal(1u, f.NumStorageBuffers);
        Assert.Equal(0u, f.NumSamplers);
        Assert.Equal(0u, f.NumStorageTextures);
        // Four cbuffers: ambient, dir light direction, dir light color, point light count.
        Assert.Equal(4u, f.NumUniformBuffers);
    }

    [Fact]
    public void LitColor_VertexShader_HasNoTexturesOrStorage()
    {
        var v = ShaderSets.LitColor.Vertex.GetResources();

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
        var f = ShaderSets.LitTexture.Fragment.GetResources();

        Assert.Equal(1u, f.NumSamplers);
        Assert.Equal(1u, f.NumStorageBuffers);
        Assert.Equal(0u, f.NumStorageTextures);
        Assert.Equal(4u, f.NumUniformBuffers);
    }

    [Fact]
    public void LitTexture_VertexShader_HasNoTexturesOrStorage()
    {
        var v = ShaderSets.LitTexture.Vertex.GetResources();

        Assert.Equal(0u, v.NumStorageBuffers);
        Assert.Equal(0u, v.NumSamplers);
        Assert.Equal(0u, v.NumStorageTextures);
        Assert.Equal(2u, v.NumUniformBuffers);
    }
}
