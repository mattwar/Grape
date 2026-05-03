using SDL3;
using Grape;

namespace Grape.Vine;

public abstract class Prop
{
    public abstract bool Update(in UpdateContext context);
    public abstract void Render(Renderer2D renderer);
}
