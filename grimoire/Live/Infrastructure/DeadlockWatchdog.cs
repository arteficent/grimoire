using System.Diagnostics;
using grimoire.Abstractions;

namespace grimoire.Infrastructure;

public sealed class DeadlockWatchdog : IDeadlockWatchdog
{
    private readonly IOutput _output;
    private readonly TimeSpan _defaultTimeout;

    public DeadlockWatchdog(IOutput output, TimeSpan? defaultTimeout = null)
    {
        _output = output;
        _defaultTimeout = defaultTimeout ?? TimeSpan.FromSeconds(3);
    }

    public WatchResult Observe(string label, Action<CancellationToken> body, TimeSpan? timeout = null)
    {
        var budget = timeout ?? _defaultTimeout;
        using var cts = new CancellationTokenSource(budget);
        using var finished = new ManualResetEventSlim(false);
        Exception? error = null;
        var stopwatch = Stopwatch.StartNew();

        // Dedicated background thread: if it deadlocks it is abandoned, and being a background
        // thread it will never keep the process alive.
        var thread = new Thread(() =>
        {
            try
            {
                body(cts.Token);
            }
            catch (Exception ex)
            {
                error = ex;
            }
            finally
            {
                finished.Set();
            }
        })
        {
            IsBackground = true,
            Name = label
        };

        thread.Start();

        // Grace period on top of the cancellation budget so cooperative work can unwind cleanly.
        var completed = finished.Wait(budget + TimeSpan.FromSeconds(1));
        stopwatch.Stop();

        return Report(label, completed, stopwatch.Elapsed, error);
    }

    public async Task<WatchResult> ObserveAsync(string label, Func<CancellationToken, Task> body, TimeSpan? timeout = null)
    {
        var budget = timeout ?? _defaultTimeout;
        using var cts = new CancellationTokenSource(budget);
        var stopwatch = Stopwatch.StartNew();

        var work = Task.Run(() => body(cts.Token), CancellationToken.None);
        var winner = await Task.WhenAny(work, Task.Delay(budget + TimeSpan.FromSeconds(1), CancellationToken.None))
                               .ConfigureAwait(false);
        stopwatch.Stop();

        var completed = ReferenceEquals(winner, work);
        var error = completed ? work.Exception?.GetBaseException() : null;

        return Report(label, completed, stopwatch.Elapsed, error);
    }

    private WatchResult Report(string label, bool completed, TimeSpan elapsed, Exception? error)
    {
        if (!completed)
        {
            _output.Error($"'{label}' did not finish within the budget -> treated as DEADLOCKED (thread abandoned).");
            return new WatchResult(WatchOutcome.Deadlocked, elapsed);
        }

        if (error is not null)
        {
            _output.Warn($"'{label}' faulted after {elapsed.TotalMilliseconds:F0} ms: {error.GetType().Name}: {error.Message}");
            return new WatchResult(WatchOutcome.Faulted, elapsed, error);
        }

        _output.Success($"'{label}' completed in {elapsed.TotalMilliseconds:F0} ms.");
        return new WatchResult(WatchOutcome.Completed, elapsed);
    }
}
