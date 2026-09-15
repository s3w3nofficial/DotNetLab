namespace DotNetLab.Lab;

/// <summary>
/// Immutable feature snapshot plus a generation counter. Async work checks
/// <see cref="IsCurrent"/> after awaits so a newer update can drop stale results.
/// </summary>
public class StateStore<T> where T : notnull
{
    private readonly object _gate = new();
    private T _value;
    private int _generation;

    public StateStore(T value) => _value = value;

    public event Action? Changed;

    public T Value
    {
        get
        {
            lock (_gate)
            {
                return _value;
            }
        }
    }

    public int Generation => Volatile.Read(ref _generation);

    public int BeginUpdate() => Interlocked.Increment(ref _generation);

    public bool IsCurrent(int generation) => generation == Volatile.Read(ref _generation);

    public void Set(T value, bool notify = false)
    {
        lock (_gate)
        {
            _value = value;
        }

        if (notify)
        {
            Changed?.Invoke();
        }
    }

    public void Update(Func<T, T> mutate, bool notify = false)
    {
        lock (_gate)
        {
            _value = mutate(_value);
        }

        if (notify)
        {
            Changed?.Invoke();
        }
    }
}
