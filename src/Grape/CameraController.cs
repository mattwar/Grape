using System.Numerics;

namespace Grape;

/// <summary>
/// Base class for camera-driving controllers — objects that own a
/// <see cref="Grape.Camera"/> and mutate it each frame (typically from
/// input or a tracked target). Plug into a render loop by calling
/// <see cref="IUpdatable{TCtx}.Update"/> with the renderer's update
/// context, then either assign <see cref="Camera"/> to
/// <see cref="Renderer3D.Camera"/> directly or call
/// <see cref="IDrawable3D.Draw"/> to do the same as a one-liner.
/// </summary>
public abstract class CameraController : IUpdatable<UpdateContext3D>, IDrawable3D
{
    /// <summary>
    /// The camera this controller drives. Defaults to a fresh
    /// <see cref="PerspectiveCamera"/>; assign your own to use a
    /// different projection or pre-configured starting pose.
    /// </summary>
    public Camera Camera { get; set; } = new PerspectiveCamera();

    /// <inheritdoc/>
    public abstract void Update(in UpdateContext3D context);

    /// <summary>
    /// Default render-side action for a controller: install
    /// <see cref="Camera"/> on the renderer. Override only if your
    /// controller also wants to issue debug draws or push other
    /// per-frame scene state.
    /// </summary>
    public virtual void Draw(Renderer3D renderer)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        renderer.Camera = Camera;
    }
}
