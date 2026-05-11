#:package Blitter@*-*

// Run this file directly with .NET 10 or later:
//
//     dotnet run samples/CustomShaderRimLight.cs
//
// While Blitter is unpublished, build a local copy first:
//
//     dotnet build src/Blitter.Package/Blitter.Package.csproj
//
// Demonstrates a Fresnel-style rim-lighting custom shader.
//
//   rim = pow(1 - saturate(dot(N, V)), rimPower)
//
// `N` is the surface normal in world space; `V` is the direction from the
// surface point back toward the camera. Faces pointing straight at the
// camera get rim ~= 0; faces grazing the silhouette get rim ~= 1. Adding
// a colored `rim * RimColor` term on top of a basic Lambertian shade is
// what gives the classic glowing-edge / "expensive-looking" outline.
//
// Shows off:
//   * A custom shader with both `Model` and `ViewProjection` matrices
//     (so the vertex stage can transform normals by the model matrix
//     alone for correct rim math).
//   * Reading the camera position from the render callback and feeding it
//     into the fragment stage so the shader can compute the view vector
//     per pixel.
//   * Mixing renderer-injected (`SetViewProjection`) with user-supplied
//     (`Model`, `CameraPos`, `RimParams`) args in the same struct.

using System.Numerics;
using System.Runtime.InteropServices;
using Blitter;
using Blitter.Bits;

// A high-tessellation sphere reads rim shading the most clearly.
var sphere = Meshes.Sphere(new Color(40, 90, 180), radius: 1.2f, latitudeSegments: 64, longitudeSegments: 96);
var torus  = Meshes.Torus(new Color(180, 60, 40), majorRadius: 0.9f, minorRadius: 0.32f);

// ----- Custom shader. Pairs with LitVertex3D (position + normal + color).

var rimShader = new Shader<LitVertex3D, RimArgs>(
    vertex: """
    cbuffer Model : register(b0, space1) { float4x4 model;          };
    cbuffer VP    : register(b1, space1) { float4x4 viewProjection; };

    struct Input
    {
        float3 Position : TEXCOORD0;
        float3 Normal   : TEXCOORD1;
        float4 Color    : TEXCOORD2;
    };

    struct Output
    {
        float3 WorldPos  : TEXCOORD0;
        float3 WorldNorm : TEXCOORD1;
        float4 Color     : TEXCOORD2;
        float4 Position  : SV_Position;
    };

    Output main(Input input)
    {
        Output output;
        float4 worldPos    = mul(model, float4(input.Position, 1.0f));
        output.WorldPos    = worldPos.xyz;
        // Upper-3x3 model is correct for rotation + uniform scale, which
        // is all this sample uses. Non-uniform scale would need a true
        // inverse-transpose normal matrix.
        output.WorldNorm   = mul((float3x3)model, input.Normal);
        output.Color       = input.Color;
        output.Position    = mul(viewProjection, worldPos);
        return output;
    }
    """,
    fragment: """
    cbuffer CameraPos : register(b0, space3) { float4 cameraPos;  }; // xyz: world-space camera position
    cbuffer Rim       : register(b1, space3) { float4 rimParams;  }; // rgb: rim color, a: rim power

    struct Input
    {
        float3 WorldPos  : TEXCOORD0;
        float3 WorldNorm : TEXCOORD1;
        float4 Color     : TEXCOORD2;
    };

    float4 main(Input input) : SV_Target0
    {
        float3 N = normalize(input.WorldNorm);
        float3 V = normalize(cameraPos.xyz - input.WorldPos);

        // Lambertian fill so the surface still has volume; one fixed
        // overhead-ish "key" light keeps this self-contained -- rim is the
        // star, the fill just gives us a base to add it onto.
        float3 keyDir = normalize(float3(0.4f, 0.8f, 0.5f));
        float NdotL   = saturate(dot(N, keyDir));
        float3 ambient = float3(0.08f, 0.10f, 0.14f);
        float3 lit     = ambient + input.Color.rgb * NdotL;

        // Fresnel-style rim. Power controls falloff: higher = thinner edge.
        float rim = pow(1.0f - saturate(dot(N, V)), rimParams.a);
        lit += rimParams.rgb * rim;

        return float4(lit, input.Color.a);
    }
    """,
    LitVertex3D.ShaderVertexLayout,
    new ShaderArgsLayout(
        new ShaderArgElement(ShaderArgStage.Vertex,   0, ShaderArgKind.Matrix4x4), // Model
        new ShaderArgElement(ShaderArgStage.Vertex,   1, ShaderArgKind.Matrix4x4), // ViewProjection
        new ShaderArgElement(ShaderArgStage.Fragment, 0, ShaderArgKind.Float4),    // CameraPos
        new ShaderArgElement(ShaderArgStage.Fragment, 1, ShaderArgKind.Float4)));  // RimParams


// ----- Window + camera + render loop
var window = new Window3D
{
    Title = "Custom shader: Fresnel rim lighting",
    BackgroundColor = new Color(2, 3, 8),
    FullScreen = true,
    CloseKey = Key.Escape,
};

var camera = new PerspectiveCamera
{
    Position = new Vector3(0f, 1.4f, 4.2f),
    Target   = new Vector3(0f, 0f, 0f),
};

window.Rendering += (w, rd) =>
{
    rd.Camera = camera;

    var t = (float)rd.ElapsedSinceStart.TotalSeconds;

    // Sphere on the left with a cool cyan rim that pulses thinner/wider.
    var sphereModel =
        Matrix4x4.CreateRotationY(t * 0.4f) *
        Matrix4x4.CreateTranslation(-1.4f, 0f, 0f);
    var rimPower = 2.5f + 1.5f * MathF.Sin(t * 0.8f); // 1.0 .. 4.0
    rd.DrawMesh(sphere, rimShader, new RimArgs
    {
        Model      = sphereModel,
        // ViewProjection filled in by the renderer via IUniformArgs.
        CameraPos  = new Vector4(camera.Position, 1f),
        RimParams  = new Vector4(0.4f, 0.9f, 1.0f, rimPower),
    });

    // Torus on the right with a warm orange rim and a fixed (thinner)
    // power so you can see how the same shader reads on different geometry.
    var torusModel =
        Matrix4x4.CreateRotationX(t * 0.6f) *
        Matrix4x4.CreateRotationY(t * 0.3f) *
        Matrix4x4.CreateTranslation(1.4f, 0f, 0f);
    rd.DrawMesh(torus, rimShader, new RimArgs
    {
        Model      = torusModel,
        CameraPos  = new Vector4(camera.Position, 1f),
        RimParams  = new Vector4(1.0f, 0.55f, 0.15f, 3.5f),
    });

    w.Invalidate();
};

await window.WaitForCloseAsync();

// ----- Per-draw args struct ------------------------------------------------
//
// Slot layout matches the ShaderArgsLayout above:
//   Vertex   slot 0 = Model           (Matrix4x4)
//   Vertex   slot 1 = ViewProjection  (Matrix4x4) -- filled by renderer
//   Fragment slot 0 = CameraPos       (Vector4, xyz used)
//   Fragment slot 1 = RimParams       (Vector4, rgb=rim color, a=rim power)
//
// Implementing `SetViewProjection` is what tells the renderer "you can
// inject your camera VP into this struct before each draw"; the caller
// then only fills the three remaining fields.

[StructLayout(LayoutKind.Sequential)]
public struct RimArgs : IUniformArgs<RimArgs>
{
    public Matrix4x4 Model;
    public Matrix4x4 ViewProjection;
    public Vector4   CameraPos;
    public Vector4   RimParams;

    public static Func<RimArgs, Matrix4x4, RimArgs>? SetViewProjection { get; } =
        (args, vp) => { args.ViewProjection = vp; return args; };
}
