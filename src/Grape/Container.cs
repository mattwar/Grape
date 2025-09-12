using System.Collections.Immutable;
using SDL3;
using SDL3.Model;

namespace Grape;

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

    public override void Render(Renderer renderer)
    {
        var props = _props;

        foreach (var prop in props)
        {
            prop.Render(renderer);
        }
    }

    public void Add(Prop prop)
    {
        ImmutableInterlocked.Update(ref _props, (list) => list.Add(prop));
    }
}
