#:package Blitter@*-*

// Run this file directly with .NET 10 or later:
//
//     dotnet run samples/CustomShaderPlasma.cs
//
// While Blitter is unpublished, build a local copy first:
//
//     dotnet build src/Blitter.Package/Blitter.Package.csproj
//
// Demonstrates a pure-fragment-shader: a classic demo plasma effect.

using System.Numerics;
using Blitter;
using Blitter.Bits;

// Build a fullscreen quad for the shader to shade.
var quad = Meshes.TexturedRectangle(new Vector2(2f, 2f));

var plasmaShader = new Shader<TextureVertex3D, PlasmaArgs>(
    vertex: 
        """
        struct Input  { float3 Position : TEXCOORD0; float2 TexCoord : TEXCOORD1; };
        struct Output { float2 TexCoord : TEXCOORD0; float4 Position : SV_Position; };

        Output main(Input input)
        {
            Output output;
            output.TexCoord = input.TexCoord;
            output.Position = float4(input.Position, 1.0f);
            return output;
        }
        """,
    fragment: 
        """
        cbuffer Frame : register(b0, space3) { float4 frame; }; // x=time, y=aspect (w/h)

        struct Input { float2 TexCoord : TEXCOORD0; };

        float4 main(Input input) : SV_Target0
        {
            float t = frame.x;
            // Center and aspect-correct the coordinate so the pattern reads round on non-square windows.
            float2 p = input.TexCoord * 2.0f - 1.0f;
            p.x *= frame.y;

            // Three rotating "sources" whose sinusoids interfere.
            float v =
                sin(p.x * 4.0f                    + t * 1.7f) +
                sin(p.y * 4.0f                    + t * 1.3f) +
                sin((p.x + p.y) * 3.0f            + t * 0.9f) +
                sin(length(p - float2(sin(t * 0.5f), cos(t * 0.4f))) * 6.0f - t * 2.1f);
            v *= 0.25f; // back into roughly -1..1

            // Cosine palette by Inigo Quilez: cheap, vibrant, parameterised
            // entirely by three vec3s. These constants give a magenta -> cyan -> yellow rainbow.
            float3 a = float3(0.50f, 0.50f, 0.50f);
            float3 b = float3(0.50f, 0.50f, 0.50f);
            float3 c = float3(1.00f, 1.00f, 1.00f);
            float3 d = float3(0.00f, 0.33f, 0.67f);
            float3 col = a + b * cos(6.2831853f * (c * v + d));

            return float4(col, 1.0f);
        }
        """,
    TextureVertex3D.ShaderVertexLayout,
    new ShaderArgsLayout(
        new ShaderArgElement(ShaderArgStage.Fragment, 0, ShaderArgKind.Float4))
    );

var window = new Window3D
{
    Title = "Custom shader: plasma",
    BackgroundColor = Color.Black,
    FullScreen = true,
    CloseKey = Key.Escape, // window close on ESC
    RelativeMouseMode = true // hide mouse
};

await window.RunAsync(rd =>
{
    var t = rd.ElapsedSecondsSinceStart;

    using (rd.PushState())
    {
        rd.CullMode = CullMode.None;

        rd.DrawMesh(quad, plasmaShader, new PlasmaArgs
        {
            Frame = new Vector4(t, rd.AspectRatio, 0f, 0f),
        });
    }
});

public struct PlasmaArgs : IUniformArgs<PlasmaArgs>
{
    public Vector4 Frame; // x=time, y=aspect, z/w reserved
}
