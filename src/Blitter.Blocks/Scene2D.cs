
using System.Collections.Immutable;

namespace Blitter.Blocks;

public class Scene2D : Container2D
{
    public Scene2D(params ImmutableList<Prop2D> props) : base(props)
    {
    }

    private Window2D? _window;

    /// <summary>
    /// Runs the scene until canceled or another exit condition is reached.
    /// </summary>
    public async Task RunAsync(Window2D window, CancellationToken cancellationToken = default)
    {
        _window = window;
        var rd = window.Renderer;

        try
        {
            // Animate the scene until exit condition
            while (!cancellationToken.IsCancellationRequested && !ShouldExit())
            {
                var context = rd.GetUpdateContext();
                this.Update(in context);
                this.Draw(rd);               
                await window.NextFrameAsync(cancellationToken);
            }       
        }
        finally
        {
            _window = null;           
        }
    }

    protected virtual bool ShouldExit()
    {
        return _window == null
            || _window.IsClosed;
    }
}
