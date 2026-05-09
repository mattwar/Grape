using System.Collections.Immutable;

namespace Blitter.Blocks;

/// <summary>
/// Stacks multiple props either vertically or horizontally.
/// </summary>
public class StackPanel2D : Prop2D
{
    private ImmutableList<Panel2D> _panels = ImmutableList<Panel2D>.Empty;
    
    public StackPanel2D(params ImmutableList<Panel2D> panels)
    {
        _panels = panels;
    }

    public bool IsVertical { get; set; } = true;

    private void ComputeLayout(in Rect bounds)
    {
        if (this.IsVertical)
        {
            var parentHeight = (int)bounds.Height;
            int fixedHeight = _panels.Where(p => p.HeightMeasure == Measure.Pixels).Sum(p => (int)p.Height);
            int remainingHeight = parentHeight - fixedHeight;
            float totalProportion = _panels.Where(p => p.HeightMeasure == Measure.Proportion).Sum(p => p.Height);
            var top = bounds.Y;

            foreach (var panel in _panels)
            {
                var height = panel.HeightMeasure switch
                {
                    Measure.Pixels => (int)panel.Height,
                    Measure.Proportion => (int)(remainingHeight * panel.Height / totalProportion),
                    _ => 0
                };
                panel.Bounds = new Rect(bounds.X, top, bounds.Width, height);
                top += height;
            }
        }
        else
        {
            var parentWidth = (int)bounds.Width;
            int fixedWidth = _panels.Where(p => p.WidthMeasure == Measure.Pixels).Sum(p => (int)p.Width);
            int remainingWidth = parentWidth - fixedWidth;
            float totalProportion = _panels.Where(p => p.WidthMeasure == Measure.Proportion).Sum(p => p.Width);
            
            var left = bounds.X;
            foreach (var panel in _panels)
            {
                var width = panel.WidthMeasure switch
                {
                    Measure.Pixels => (int)panel.Width,
                    Measure.Proportion => (int)(remainingWidth * panel.Height / totalProportion),
                    _ => 0
                };
                panel.Bounds = new Rect(left, bounds.Y, width, bounds.Height);
                left += width;
            }
        }
    }

    public override bool Update(in UpdateContext2D context)
    {
        ComputeLayout(context.Bounds);

        var panels = _panels;
        bool changed = false;

        foreach (var panel in panels)
        {
            var panelContext = new UpdateContext2D
            {
                ElapsedSinceStart = context.ElapsedSinceStart,
                ElapsedSinceLastUpdate = context.ElapsedSinceLastUpdate,
                Bounds = panel.Bounds
            };

            if (panel.Prop.Update(panelContext))
                changed = true;
        }

        return changed;
    }

    public override void Draw(Renderer2D renderer)
    {
        var originalClip = renderer.ClipRect;

        var panels = _panels;
        foreach (var panel in panels)
        {
            renderer.ClipRect = panel.Bounds;
            panel.Prop.Draw(renderer);
        }
    }
}
