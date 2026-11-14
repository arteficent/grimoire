using System.Collections.Concurrent;

namespace grimoire.Infrastructure;

/// <summary>
/// Mimics a UI / classic ASP.NET context: every continuation is marshalled back onto one
/// single thread. That is the ingredient that turns <c>.Result</c> into a deadlock.
/// </summary>
public sealed class SingleThreadedSynchronizationContext : SynchronizationContext, IDisposable
{
    private readonly BlockingCollection<(SendOrPostCallback Callback, object? State)> _queue = new();

    public override void Post(SendOrPostCallback d, object? state)
    {
        if (!_queue.IsAddingCompleted)
        {
            _queue.Add((d, state));
        }
    }

    public override void Send(SendOrPostCallback d, object? state) => d(state);

    /// <summary>Pumps queued continuations until <paramref name="until"/> completes or the budget expires.</summary>
    public void RunOnCurrentThread(Task until, TimeSpan budget)
    {
        var deadline = DateTime.UtcNow + budget;

        while (!until.IsCompleted && DateTime.UtcNow < deadline)
        {
            if (_queue.TryTake(out var item, millisecondsTimeout: 50))
            {
                item.Callback(item.State);
            }
        }
    }

    public void Dispose()
    {
        _queue.CompleteAdding();
        _queue.Dispose();
    }
}
