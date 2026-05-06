namespace Grape.Jelly;

public abstract class Prop
{
    public abstract bool Update(in UpdateContext context);
    public abstract void Draw(Renderer2D renderer);
}
