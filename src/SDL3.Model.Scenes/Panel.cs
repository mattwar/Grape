using System.Collections.Immutable;

namespace SDL3.Model.Scenes;

public class Panel
{
    public Panel(Prop prop)
    {
        this.Prop = prop;
        this.Prop = prop;
    }

    public Prop Prop { get; }

    /// <summary>
    /// The computed bounds of the panel within the window.
    /// </summary>
    internal SDL.Rect Bounds { get; set; }

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
    /// Converts a <see cref="Prop"/> to a <see cref="Panel"/>.
    /// </summary>
    public static implicit operator Panel(Prop prop) => new Panel(prop);
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
public class StackPanel : Prop
{
    private ImmutableList<Panel> _panels = ImmutableList<Panel>.Empty;
    
    public StackPanel(ImmutableList<Panel> panels)
    {
        _panels = panels;
    }

    public bool IsVertical { get; set; } = true;

    private void ComputeLayout(in SDL.Rect bounds)
    {
        if (this.IsVertical)
        {
            var parentHeight = bounds.H;
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
                panel.Bounds = new SDL.Rect
                {
                    X = bounds.X,
                    Y = top,
                    W = bounds.W,
                    H = height
                };
                top += height;
            }
        }
        else
        {
            var parentWidth = bounds.W;
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
                panel.Bounds = new SDL.Rect
                {
                    X = left,
                    Y = bounds.Y,
                    W = width,
                    H = bounds.H
                };
                left += width;
            }
        }
    }

    public override bool Update(in UpdateContext context)
    {
        ComputeLayout(context.Bounds);

        var panels = _panels;
        bool changed = false;

        foreach (var panel in panels)
        {
            var panelContext = new UpdateContext
            {
                Time = context.Time,
                Bounds = panel.Bounds
            };

            if (panel.Prop.Update(panelContext))
                changed = true;
        }

        return changed;
    }

    public override void Render(Renderer renderer)
    {
        var originalClip = renderer.ClipRect;

        var panels = _panels;
        foreach (var panel in panels)
        {
            renderer.ClipRect = panel.Bounds;
            panel.Prop.Render(renderer);
        }
    }
}

/// <summary>
/// Overlays multiple props on top of each other.
/// </summary>
public class OverlayPanel : Prop
{
    private ImmutableList<Prop> _layers = ImmutableList<Prop>.Empty;

    public OverlayPanel(ImmutableList<Prop> layers)
    {
        _layers = layers;
    }

    public ImmutableList<Prop> Layers => _layers;

    public void AddLayer(Prop layer)
    {
        ImmutableInterlocked.Update(ref _layers, _layers => _layers.Add(layer));
    }

    public void RemoveLayer(Prop layer)
    {
        ImmutableInterlocked.Update(ref _layers, _layers => _layers.Remove(layer));
    }

    public override bool Update(in UpdateContext context)
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

    public override void Render(Renderer renderer)
    {
        var originalClip = renderer.ClipRect;
        var panels = _layers;
        foreach (var panel in panels)
        {
            panel.Render(renderer);
        }
    }
}