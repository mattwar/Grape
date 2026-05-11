#:package Blitter@*-*

// Run this file directly with .NET 10 or later:
//
//     dotnet run samples/CustomShaderPlasma.cs
//
// While Blitter is unpublished, build a local copy first:
//
//     dotnet build src/Blitter.Package/Blitter.Package.csproj
//
// Demonstrates a pure-fragment-shader effect: a classic demoscene plasma.
//
// All the visual interest happens in the fragment stage. The vertex stage
// just passes through a fullscreen quad already specified in clip space,
// and the fragment computes per-pixel color from a sum of sinusoids of
// the UV coordinate plus time, then maps the result through a small
// palette. No mesh, lighting, or geometry tricks involved -- this is the
// "what can a fragment shader do on its own" advertisement.

using System.Numerics;
using System.Runtime.InteropServices;
using Blitter;

// ----- Fullscreen quad in NDC (-1..1 on X and Y, z=0). UVs go 0..1
// across so the fragment shader has a built-in coordinate to work with.
var quad = Mesh.Create<TextureVertex3D>(
    [
        new(new Vector3(-1f, -1f, 0f), new Vector2(0f, 0f)),
        new(new Vector3( 1f, -1f, 0f), new Vector2(1f, 0f)),
        new(new Vector3( 1f,  1f, 0f), new Vector2(1f, 1f)),
        new(new Vector3(-1f,  1f, 0f), new Vector2(0f, 1f)),
    ],
    [0, 1, 2, 0, 2, 3]
    );

// ----- Custom shader.
var plasmaShader = new Shader<TextureVertex3D, PlasmaArgs>(
    vertex: """
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
    // Plasma: sum a few sinusoids of the UV coordinate, scaled and offset by
    // time, then map the sum through a 3-channel cosine-based palette. The
    // "moves diagonally across the screen" feel comes from offsetting each
    // term by a different multiple of t.
    fragment: """
    cbuffer Frame : register(b0, space3) { float4 frame; }; // x=time, y=aspect (w/h)

    struct Input { float2 TexCoord : TEXCOORD0; };

    float4 main(Input input) : SV_Target0
    {
        float t = frame.x;
        // Centre and aspect-correct the coordinate so the pattern reads
        // round on non-square windows.
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
        // entirely by three vec3s. These constants give a magenta -> cyan
        // -> yellow rainbow.
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

// ----- Window + render loop. No camera needed: the vertex stage emits
// clip-space positions directly.

var window = new Window3D
{
    Title = "Custom shader: plasma",
    BackgroundColor = Color.Black,
    FullScreen = true,
    CloseKey = Key.Escape,
};

window.Rendering += (w, rd) =>
{
    var t = (float)rd.ElapsedSinceStart.TotalSeconds;
    var (width, height) = w.Size;
    var aspect = width / (float)height;

    using (rd.PushState())
    {
        // The quad covers the whole screen and ignores depth, so disable
        // back-face culling to avoid surprises if the viewer ever flips
        // a winding convention.
        rd.CullMode = CullMode.None;

        rd.DrawMesh(quad, plasmaShader, new PlasmaArgs
        {
            Frame = new Vector4(t, aspect, 0f, 0f),
        });
    }

    w.Invalidate();
};

await window.WaitForCloseAsync();

// ----- Per-draw args struct ------------------------------------------------
//
// Single fragment-stage slot carrying time + aspect ratio. No
// IUniformArgs accessors -- this shader doesn't need any
// renderer-injected state.

[StructLayout(LayoutKind.Sequential)]
public struct PlasmaArgs : IUniformArgs<PlasmaArgs>
{
    public Vector4 Frame; // x=time, y=aspect, z/w reserved
}
