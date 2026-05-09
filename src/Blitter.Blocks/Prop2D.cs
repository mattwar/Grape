namespace Blitter.Blocks;

public abstract class Prop2D : IUpdatable<UpdateContext2D>, IDrawable2D
{
    /// <summary>Advance one tick. Return false to indicate this prop wants to be removed.</summary>
    public abstract bool Update(in UpdateContext2D context);

    /// <summary>Issue draws for this prop's current state.</summary>
    public abstract void Draw(Renderer2D renderer);

    // IUpdatable<TCtx> contract: forwards to the bool-returning override.
    // The Container/Scene loop reads the bool to drive lifecycle; the
    // generic interface contract is "advance once" and ignores the return.
    void IUpdatable<UpdateContext2D>.Update(in UpdateContext2D context) => Update(context);
}
