using System.Collections.Immutable;

namespace Grape.Jelly;

public abstract class Container2D : Prop2D
{
    private ImmutableList<Prop2D> _props = ImmutableList<Prop2D>.Empty;

    public ImmutableList<Prop2D> Props => _props;

    public Container2D(ImmutableList<Prop2D> props)
    {
        _props = props;
    }

    public override bool Update(in UpdateContext2D context)
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

    public void Add(Prop2D prop)
    {
        ImmutableInterlocked.Update(ref _props, (list) => list.Add(prop));
    }
}
