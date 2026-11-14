using System.Collections.Concurrent;
using grimoire.Abstractions;

namespace grimoire.Scenarios.Deadlocks;

/// <summary>Every worker of a bounded pool is blocked on work that only that pool can run.</summary>
public sealed class ThreadPoolStarvationScenario : ScenarioBase
{
    public override string Id => "D5";

    public override string Title => "Worker-pool starvation deadlock";

    public override string Description =>
        "A pool with two workers. Each queued item blocks waiting for another item that is still in the queue, so no worker is left to run it.";

    public override ScenarioCategory Category => ScenarioCategory.Deadlock;

    protected override string Takeaway =>
        "A resource deadlock with no lock involved: the scarce resource is the worker thread. The real ThreadPool only hides it by injecting extra threads slowly, which shows up as seconds-long latency spikes before it recovers.";

    protected override Task ExecuteAsync(IScenarioContext context, CancellationToken cancellationToken)
    {
        context.Output.Step("Blocking a pool worker on work queued back to the same pool:");
        using (var pool = new BoundedWorkerPool(2))
        {
            context.Watchdog.Observe("starved-pool", _ =>
            {
                var outer = Enumerable.Range(1, 2).Select(i => pool.Enqueue(() =>
                {
                    context.Output.Step($"outer {i} occupies a worker and blocks on an inner item");
                    pool.Enqueue(() => context.Output.Step($"inner {i} ran")).Wait();
                })).ToArray();

                Task.WaitAll(outer);
            }, TimeSpan.FromSeconds(3));

            context.Output.Warn($"Both workers are blocked; {pool.QueuedCount} inner items are queued and can never start.");
        }

        context.Output.Blank();
        context.Output.Step("Same workload without re-entrant queueing (the inner work runs inline):");

        using (var pool = new BoundedWorkerPool(2))
        {
            context.Watchdog.Observe("healthy-pool", _ =>
            {
                var outer = Enumerable.Range(1, 2).Select(i => pool.Enqueue(() =>
                {
                    context.Output.Step($"outer {i} started");
                    context.Output.Step($"inner {i} ran");
                })).ToArray();

                Task.WaitAll(outer);
            }, TimeSpan.FromSeconds(3));
        }

        return Task.CompletedTask;
    }

    /// <summary>A deliberately tiny, fixed-size pool so starvation is deterministic rather than timing dependent.</summary>
    private sealed class BoundedWorkerPool : IDisposable
    {
        private readonly BlockingCollection<Action> _queue = new();
        private readonly Thread[] _workers;

        public BoundedWorkerPool(int size)
        {
            _workers = Enumerable.Range(0, size).Select(i =>
            {
                var worker = new Thread(WorkerLoop) { IsBackground = true, Name = $"pool-worker-{i}" };
                worker.Start();
                return worker;
            }).ToArray();
        }

        public int QueuedCount => _queue.Count;

        public Task Enqueue(Action work)
        {
            var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            _queue.Add(() =>
            {
                try
                {
                    work();
                    completion.TrySetResult();
                }
                catch (Exception ex)
                {
                    completion.TrySetException(ex);
                }
            });

            return completion.Task;
        }

        private void WorkerLoop()
        {
            foreach (var work in _queue.GetConsumingEnumerable())
            {
                work();
            }
        }

        public void Dispose()
        {
            _queue.CompleteAdding();
            foreach (var worker in _workers)
            {
                worker.Join(TimeSpan.FromMilliseconds(200));
            }
            // Not disposing _queue: starved workers may still be parked inside it.
        }
    }
}
