#:package Blitter@*-*

// Run this file directly with .NET 10 or later:
//
//     dotnet run samples/CustomShaderWavePlane.cs
//
// While Blitter is unpublished, build a local copy first:
//
//     dotnet build src/Blitter.Package/Blitter.Package.csproj
//
// Demonstrates a custom shader that makes a surface wave:

// The plane is a manually built grid of `Vertex3D` (position only): the
// vertex shader does *all* the displacement, so the CPU mesh stays static
// and the wave runs entirely on the GPU.

using System.Numerics;
using Blitter;

const int subdivisions = 96;       // grid resolution along each axis
const float planeSize  = 12f;      // world-space side length of the plane

// Build a tessellated plane of Vertex3D (position only). 
// The wave happens in the vertex shader, so we just need a flat grid here.
var verts = new Vertex3D[(subdivisions + 1) * (subdivisions + 1)];
for (int z = 0; z <= subdivisions; z++)
{
    for (int x = 0; x <= subdivisions; x++)
    {
        float fx = (x / (float)subdivisions - 0.5f) * planeSize;
        float fz = (z / (float)subdivisions - 0.5f) * planeSize;
        verts[z * (subdivisions + 1) + x] = new Vertex3D(fx, 0f, fz);
    }
}

// Two triangles per cell, indexed.
var indices = new uint[subdivisions * subdivisions * 6];
int wi = 0;
for (int z = 0; z < subdivisions; z++)
{
    for (int x = 0; x < subdivisions; x++)
    {
        uint i00 = (uint)(z * (subdivisions + 1) + x);
        uint i10 = (uint)(z * (subdivisions + 1) + x + 1);
        uint i01 = (uint)((z + 1) * (subdivisions + 1) + x);
        uint i11 = (uint)((z + 1) * (subdivisions + 1) + x + 1);
        indices[wi++] = i00; indices[wi++] = i01; indices[wi++] = i11;
        indices[wi++] = i00; indices[wi++] = i11; indices[wi++] = i10;
    }
}

var plane = Mesh.Create(verts, indices);

// ----- Custom shader. Position-only vertex layout in, animated wave +
// height-coloured fragments out.

var waveShader = new Shader<Vertex3D, WaveArgs>(
    vertex: 
        """
        cbuffer VP   : register(b0, space1) { float4x4 viewProjection; };
        cbuffer Wave : register(b1, space1) { float4 waveParams;       }; // x=time, y=amplitude, z=frequency

        struct Input  { float3 Position : TEXCOORD0; };
        struct Output { float Height : TEXCOORD0; float4 Position : SV_Position; };

        Output main(Input input)
        {
            float t   = waveParams.x;
            float amp = waveParams.y;
            float k   = waveParams.z;

            // Two interfering sine waves running at slightly different speeds
            // produce travelling crests instead of a static standing pattern.
            float h =
                sin(input.Position.x * k          + t * 1.3f) * amp +
                cos(input.Position.z * k * 0.85f  + t * 0.9f) * amp * 0.6f;

            float3 displaced = float3(input.Position.x, h, input.Position.z);

            Output output;
            output.Height   = h;
            output.Position = mul(viewProjection, float4(displaced, 1.0f));
            return output;
        }
        """,
    fragment: 
        """
        struct Input { float Height : TEXCOORD0; };

        float4 main(Input input) : SV_Target0
        {
            // Height -> 0..1 across the wave's range, then map through a
            // simple two-stop palette (deep ocean blue -> warm crest white).
            float t      = saturate(input.Height * 0.5f + 0.5f);
            float3 trough = float3(0.04f, 0.10f, 0.40f);
            float3 crest  = float3(0.85f, 0.98f, 1.00f);
            float3 col   = lerp(trough, crest, t);

            // Subtle banding at the crests for an extra sense of motion.
            float band = smoothstep(0.65f, 0.85f, t);
            col = lerp(col, float3(1.0f, 1.0f, 1.0f), band * 0.35f);

            return float4(col, 1.0f);
        }
        """,
    Vertex3D.ShaderVertexLayout,
    new ShaderArgsLayout(
        new ShaderArgElement(ShaderArgStage.Vertex, 0, ShaderArgKind.Matrix4x4),
        new ShaderArgElement(ShaderArgStage.Vertex, 1, ShaderArgKind.Float4))
    );

var window = new Window3D
{
    Title = "Custom shader: wave plane",
    BackgroundColor = new Color(4, 6, 18),
    FullScreen = true,
    CloseKey = Key.Escape,
};

var camera = new PerspectiveCamera
{
    Position = new Vector3(0f, 4.5f, 9f),
    Target = new Vector3(0f, 0f, 0f),
};

await window.RunAsync(rd =>
{
    rd.Camera = camera;

    var t = rd.ElapsedSecondsSinceStart;

    using (rd.PushState())
    {
        rd.CullMode = CullMode.None;

        rd.DrawMesh(plane, waveShader, new WaveArgs
        {
            // ViewProjection is filled in by the renderer via IUniformArgs.
            WaveParams = new Vector4(
                t,             // time
                0.45f,         // amplitude
                0.85f,         // frequency
                0f),
        });
    }
});

public struct WaveArgs : IUniformArgs<WaveArgs>
{
    public Matrix4x4 ViewProjection;
    public Vector4   WaveParams;

    public static Func<WaveArgs, Matrix4x4, WaveArgs>? SetViewProjection { get; } =
        (args, vp) => { args.ViewProjection = vp; return args; };
}
