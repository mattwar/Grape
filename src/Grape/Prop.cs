using SDL3;
using SDL3.Model;

namespace Grape;

public abstract class Prop
{
    public abstract bool Update(in UpdateContext context);
    public abstract void Render(Renderer renderer);
}
