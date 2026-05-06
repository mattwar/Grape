using System.Collections.Immutable;

namespace Grape.Jelly;

public abstract class Container : Prop
{
    private ImmutableList<Prop> _props = ImmutableList<Prop>.Empty;

    public Container(ImmutableList<Prop> props)
    {
        _props = props;
    }

    public override bool Update(in UpdateContext context)
    {
        var changed = false;
        var props = _props;

        foreach (var prop in props)
        {
            if (prop.Update(context))
            {
                changed = true;
            }
        }

        return changed;
    }

    public override void Draw(Renderer2D renderer)
    {
        var props = _props;

        foreach (var prop in props)
        {
            prop.Draw(renderer);
        }
    }

    public void Add(Prop prop)
    {
        ImmutableInterlocked.Update(ref _props, (list) => list.Add(prop));
    }
}
