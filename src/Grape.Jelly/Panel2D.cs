using System.Collections.Immutable;

namespace Grape.Jelly;

public class Panel2D
{
    public Panel2D(Prop2D prop)
    {
        this.Prop = prop;
        this.Prop = prop;
    }

    public Prop2D Prop { get; }

    /// <summary>
    /// The computed bounds of the panel within the window.
    /// </summary>
    internal Rect Bounds { get; set; }

    /// <summary>
    /// The measure type for Width.
    /// </summary>
    public Measure WidthMeasure { get; set; } = Measure.Proportion;

    /// <summary>
    /// The measure type for Height.
    /// </summary>
    public Measure HeightMeasure { get; set; } = Measure.Pixels;

    /// <summary>
    /// The width of the panel.
    /// </summary>
    public float Width { get; set; } = 100f;

    /// <summary>
    /// The height of the panel
    /// </summary>
    public float Height { get; set; } = 100f;

    /// <summary>
    /// Converts a <see cref="Prop"/> to a <see cref="Panel2D"/>.
    /// </summary>
    public static implicit operator Panel2D(Prop2D prop) => new Panel2D(prop);
}

public enum Measure
{
    /// <summary>
    /// The dimension is specified in pixels.
    /// </summary>
    Pixels,

    /// <summary>
    /// The dimension is based on a proportion of the remaining space in the parent container after fixed dimensions are allocated.
    /// </summary>
    Proportion
}

/// <summary>
/// Stacks multiple props either vertically or horizontally.
/// </summary>
public class StackPanel2D : Prop2D
{
    private ImmutableList<Panel2D> _panels = ImmutableList<Panel2D>.Empty;
    
    public StackPanel2D(ImmutableList<Panel2D> panels)
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

/// <summary>
/// Overlays multiple props on top of each other.
/// </summary>
public class OverlayPanel2D : Prop2D
{
    private ImmutableList<Prop2D> _layers = ImmutableList<Prop2D>.Empty;

    public OverlayPanel2D(ImmutableList<Prop2D> layers)
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
