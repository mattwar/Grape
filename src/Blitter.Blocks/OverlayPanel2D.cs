using System.Collections.Immutable;

namespace Blitter.Blocks;

/// <summary>
/// Overlays multiple props on top of each other.
/// </summary>
public class OverlayPanel2D : Prop2D
{
    private ImmutableList<Prop2D> _layers = ImmutableList<Prop2D>.Empty;

    public OverlayPanel2D(params ImmutableList<Prop2D> layers)
    {
        _layers = layers;
    }

    public ImmutableList<Prop2D> Layers => _layers;

    public void AddLayer(Prop2D layer)
    {
        ImmutableInterlocked.Update(ref _layers, _layers => _layers.Add(layer));
    }

    public void RemoveLayer(Prop2D layer)
    {
        ImmutableInterlocked.Update(ref _layers, _layers => _layers.Remove(layer));
    }

    public override bool Update(in UpdateContext2D context)
    {
        var panels = _layers;
        bool changed = false;

        foreach (var panel in panels)
        {
            if (Update(context))
                changed = true;
        }

        return changed;
    }

    public override void Draw(Renderer2D renderer)
    {
        var originalClip = renderer.ClipRect;
        var panels = _layers;
        foreach (var panel in panels)
        {
            panel.Draw(renderer);
        }
    }
}
