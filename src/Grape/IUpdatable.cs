namespace Grape;

/// <summary>
/// Implemented by stateful objects that advance once per frame from a
/// per-frame inputs context.
/// </summary>
/// <typeparam name="TCtx">
/// The context shape this object needs. Constraining to a specific
/// interface (e.g. <see cref="IUpdateContext2D"/>) lets the
/// implementation receive typed extras without boxing.
/// </typeparam>
public interface IUpdatable<TCtx> where TCtx : IUpdateContext
{
    /// <summary>Advance one tick.</summary>
    void Update(in TCtx context);
}

/// <summary>
/// Implemented by objects that issue draws against a 2D renderer.
/// Pair with <see cref="IUpdatable{TCtx}"/> when the object also has
/// per-frame state to advance.
/// </summary>
public interface IDrawable2D
{
    void Draw(Renderer2D renderer);
}

/// <summary>
/// Implemented by objects that issue draws against (or otherwise
/// manipulate the state of) a 3D renderer. A controller that only
/// pushes scene state (camera assignment, light registration) and
/// emits no geometry is still a valid <see cref="IDrawable3D"/>.
/// </summary>
public interface IDrawable3D
{
    void Draw(Renderer3D renderer);
}
