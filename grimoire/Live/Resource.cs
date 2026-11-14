using System.Collections.Concurrent;

namespace grimoire;

/// <summary>
/// Common shape for the shared state used by the data-structure scenarios, so the exact same
/// hammering routine can be pointed at a thread-unsafe and a thread-safe implementation.
/// </summary>
public interface ISharedResource
{
    string Name { get; }

    void Add(int value);

    int Count { get; }

    long Sum { get; }
}

/// <summary>Plain BCL collections, no synchronisation: races, lost updates and corruption.</summary>
public sealed class ThreadUnsafeResource : ISharedResource
{
    private readonly List<int> _list = new();
    private readonly Dictionary<int, int> _dictionary = new();
    private readonly Queue<int> _queue = new();

    public string Name => "ThreadUnsafeResource (List / Dictionary / Queue)";

    public void Add(int value)
    {
        _list.Add(value);
        _dictionary[value] = value;
        _queue.Enqueue(value);
    }

    public int Count => _list.Count;

    public long Sum
    {
        get
        {
            long total = 0;
            foreach (var item in _list)
            {
                total += item;
            }

            return total;
        }
    }
}

/// <summary>Same API, made safe with a lock for the list and concurrent types elsewhere.</summary>
public sealed class ThreadSafeResource : ISharedResource
{
    private readonly object _gate = new();
    private readonly List<int> _list = new();
    private readonly ConcurrentDictionary<int, int> _dictionary = new();
    private readonly ConcurrentQueue<int> _queue = new();

    public string Name => "ThreadSafeResource (lock + ConcurrentDictionary + ConcurrentQueue)";

    public void Add(int value)
    {
        lock (_gate)
        {
            _list.Add(value);
        }

        _dictionary[value] = value;
        _queue.Enqueue(value);
    }

    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _list.Count;
            }
        }
    }

    public long Sum
    {
        get
        {
            lock (_gate)
            {
                long total = 0;
                foreach (var item in _list)
                {
                    total += item;
                }

                return total;
            }
        }
    }
}
