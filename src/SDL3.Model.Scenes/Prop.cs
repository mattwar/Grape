namespace SDL3.Model.Scenes;

public abstract class Prop
{
    public abstract bool Update(in UpdateContext context);
    public abstract void Render(Renderer renderer);
}
