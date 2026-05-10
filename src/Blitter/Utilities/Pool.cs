namespace Blitter.Utilities;

internal class Pool<T> where T : class
{
    private readonly Func<Pool<T>, T> _factory;
    private readonly Stack<T> _stack = new();

    public Pool(Func<Pool<T>, T> factory)
    {
        _factory = factory;
    }

    public T Allocate()
    {
        if (_stack.Count > 0)
            return _stack.Pop();
        else
            return _factory(this);
    }

    public void Return(T item)
    {
        ArgumentNullException.ThrowIfNull(item);
        _stack.Push(item);
    }
}

internal class PoolMap
{
    private readonly Dictionary<Type, object> _pools = new();

    public Pool<T> GetPool<T>(Func<Pool<T>, T> factory) where T : class
    {
        var type = typeof(T);
        if (!_pools.TryGetValue(type, out var poolObj))
        {
            var pool = new Pool<T>(factory);
            _pools[type] = pool;
            return pool;
        }
        else
        {
            return (Pool<T>)poolObj;
        }
    }
}