
using System.Collections.Immutable;

namespace Grape.Jelly;

public class Scene //: IDisposable
{
    private ImmutableList<Prop> _props = ImmutableList<Prop>.Empty;
    //private readonly AsyncPeriodicEvent _updateEvent;

    public Scene(ImmutableList<Prop> props)
    {
        _props = props;
        //_updateEvent = new AsyncPeriodicEvent(TimeSpan.FromMicroseconds(100), this.Update);
    }

    //public void Dispose()
    //{
    //    //_ = StopUpdatingAsync();
    //}

    ///// <summary>
    ///// How often the scene is updated. Default is 16ms.
    ///// </summary>
    //public TimeSpan UpdatePeriod
    //{
    //    get => _updateEvent.Period;
    //    set => _updateEvent.Period = value;
    //}

    ///// <summary>
    ///// Starts the scene progressing.
    ///// </summary>
    //public virtual void StartUpdating()
    //{
    //    _updateEvent.Start();
    //}

    ///// <summary>
    ///// Stops the scene from progressing.
    ///// </summary>
    //public virtual Task StopUpdatingAsync()
    //{
    //    return _updateEvent.StopAsync();
    //}

    //public Action<TimeSpan> Updated;

    /// <summary>
    /// Updates the scene state.
    /// Returns true if the scene changed.
    /// </summary>
    public bool Update(UpdateContext context, CancellationToken cancellationToken = default)
    {
        var changed = false;

        //var size = this.Window.Size;
        //var context = new UpdateContext
        //{
        //    Time = time,
        //    Bounds = new Rect(0, 0, size.Width, size.Height)
        //};

        foreach (var prop in _props)
        {
            if (cancellationToken.IsCancellationRequested)
                return false;

            if (prop.Update(context))
            {
                changed = true;
            }
        }

        return changed;
    }

    /// <summary>
    /// Renders all the props in the scene.
    /// </summary>
    public void Draw(Renderer2D renderer)
    {
        foreach (var prop in _props)
        {
            prop.Draw(renderer);
        }
    }

    public void AddProp(Prop prop)
    {
        ImmutableInterlocked.Update(ref _props, (list) => list.Add(prop));
    }
}
