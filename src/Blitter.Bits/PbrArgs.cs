using System.Numerics;
using System.Runtime.InteropServices;

namespace Blitter.Bits;

/// <summary>
/// Per-draw arguments for <see cref="PbrShaders.LitPbr"/>. Carries the
/// model matrix and per-material PBR factors; lighting fields are
/// filled in by the renderer through <see cref="IUniformArgs{TSelf}"/>.
/// </summary>
/// <remarks>
/// Field order is part of the contract: the matching
/// <see cref="ShaderArgsLayout"/> walks fields in declaration order, so
/// rearranging anything desyncs the uniform pushes. The four
/// material-side <see cref="Vector4"/> fields are pushed as a single
/// 64-byte cbuffer at fragment slot 0 to stay within SDL_GPU's 4
/// uniform-buffers-per-stage limit; the point-light count is packed
/// into <see cref="LightDirection"/>'s <c>.w</c> for the same reason.
/// </remarks>
[StructLayout(LayoutKind.Sequential)]
public struct PbrArgs : IUniformArgs<PbrArgs>
{
    /// <summary>Per-draw world transform. Caller-supplied.</summary>
    public Matrix4x4 Model;

    /// <summary>Camera view-projection. Filled in by the renderer.</summary>
    public Matrix4x4 ViewProjection;

    // ---- Material block: 4 contiguous vec4s, pushed as one 64-byte
    // cbuffer to fragment slot 0.

    /// <summary>Linear base color factor (RGBA). Multiplied with the base color sample.</summary>
    public Vector4 BaseColorFactor;

    /// <summary>x = metallic, y = roughness, z = occlusion strength, w = specular max mip (filled by renderer).</summary>
    public Vector4 MaterialFactors;

    /// <summary>
    /// Linear emissive factor (RGB). The <c>w</c> channel carries the
    /// environment yaw (radians) for IBL cubemap rotation -- packed
    /// here because SDL_GPU caps fragment cbuffers at 4 and the material
    /// block already uses one.
    /// </summary>
    public Vector4 EmissiveFactor;

    /// <summary>
    /// Camera world-space position (xyz; w unused). Filled in by the
    /// renderer; lives in the material block to keep the shader within
    /// SDL_GPU's 4 fragment cbuffers.
    /// </summary>
    public Vector4 CameraPosition;

    // ---- Lighting: separate 16-byte cbuffers at fragment slots 1..3.

    /// <summary>Ambient color (RGBA, 0..1). Filled in by the renderer.</summary>
    public Vector4 AmbientLight;

    /// <summary>
    /// Directional light direction in world space (xyz). The
    /// <c>.w</c> channel is overwritten by the renderer with the
    /// point-light count -- a packing trick to stay within SDL_GPU's
    /// 4 fragment cbuffers.
    /// </summary>
    public Vector4 LightDirection;

    /// <summary>Directional light color (RGBA, 0..1). Filled in by the renderer.</summary>
    public Vector4 LightColor;

    /// <inheritdoc cref="IUniformArgs{TSelf}.SetViewProjection"/>
    public static Func<PbrArgs, Matrix4x4, PbrArgs>? SetViewProjection { get; } =
        (a, vp) => { a.ViewProjection = vp; return a; };

    /// <inheritdoc cref="IUniformArgs{TSelf}.SetAmbientLight"/>
    public static Func<PbrArgs, Vector4, PbrArgs>? SetAmbientLight { get; } =
        (a, amb) => { a.AmbientLight = amb; return a; };

    /// <inheritdoc cref="IUniformArgs{TSelf}.SetDirectionalLight"/>
    public static Func<PbrArgs, DirectionalLight, PbrArgs>? SetDirectionalLight { get; } =
        (a, light) =>
        {
            // Preserve LightDirection.W (point-light count) -- the
            // two setters fire in an unspecified order and must commute.
            a.LightDirection = new Vector4(light.Direction, a.LightDirection.W);
            a.LightColor = light.Color;
            return a;
        };

    /// <inheritdoc cref="IUniformArgs{TSelf}.SetPointLightCount"/>
    public static Func<PbrArgs, int, PbrArgs>? SetPointLightCount { get; } =
        (a, count) => { a.LightDirection.W = count; return a; };

    /// <inheritdoc cref="IUniformArgs{TSelf}.SetCameraPosition"/>
    public static Func<PbrArgs, Vector3, PbrArgs>? SetCameraPosition { get; } =
        (a, pos) => { a.CameraPosition = new Vector4(pos, 0f); return a; };
}
