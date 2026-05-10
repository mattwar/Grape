
using System.Collections.Immutable;

namespace Blitter.Blocks;

public class Scene2D : Container2D
{
    public Scene2D(params ImmutableList<Prop2D> props) : base(props)
    {
    }

    private Window2D? _window;

    /// <summary>
    /// Runs the scene on a dedicated render thread until canceled or
    /// another exit condition is reached. The returned task completes
    /// when the loop exits, so multiple scenes / windows can be
    /// composed via <see cref="Task.WhenAll(Task[])"/>.
    /// </summary>
    public async Task RunAsync(Window2D window, CancellationToken cancellationToken = default)
    {
        _window = window;

        try
        {
            await window.RunAsync(
                shouldContinue: () => !ShouldExit(),
                renderFrame: rd =>
                {
                    var context = rd.GetUpdateContext();
                    this.Update(in context);
                    this.Draw(rd);
                },
                cancellationToken);
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
