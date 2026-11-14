using System.Diagnostics;
using grimoire.Abstractions;
using grimoire.Infrastructure;

namespace grimoire.Scenarios.Synchronization;

/// <summary>Same read-heavy workload through five different primitives, timed.</summary>
public sealed class PrimitiveComparisonScenario : ScenarioBase
{
    private const int Readers = 6;
    private const int Writers = 2;
    private const int Operations = 50_000;

    public override string Id => "S2";

    public override string Title => "Primitive tour: lock, SemaphoreSlim, ReaderWriterLockSlim, SpinLock, Interlocked";

    public override string Description =>
        "A read-heavy shared counter guarded by each primitive in turn, so the cost and the semantics can be compared side by side.";

    public override ScenarioCategory Category => ScenarioCategory.Synchronization;

    protected override string Takeaway =>
        "Mutual exclusion is the wrong default for read-heavy state: ReaderWriterLockSlim allows parallel readers, Interlocked avoids blocking entirely, SpinLock only pays off for very short critical sections.";

    private interface IGuard : IDisposable
    {
        string Name { get; }

        int Read();

        void Write(int value);
    }

    protected override Task ExecuteAsync(IScenarioContext context, CancellationToken cancellationToken)
    {
        IGuard[] guards =
        [
            new MonitorGuard(),
            new SemaphoreGuard(),
            new ReaderWriterGuard(),
            new SpinLockGuard(),
            new InterlockedGuard()
        ];

        foreach (var guard in guards)
        {
            using (guard)
            {
                Measure(context, guard);
            }
        }

        return Task.CompletedTask;
    }

    private static void Measure(IScenarioContext context, IGuard guard)
    {
        var stopwatch = Stopwatch.StartNew();

        var readers = Enumerable.Range(0, Readers).Select(i => ThreadHelper.StartBackground($"r{i}", () =>
        {
            var sink = 0;
            for (var n = 0; n < Operations; n++)
            {
                sink += guard.Read();
            }

            GC.KeepAlive(sink);
        }));

        var writers = Enumerable.Range(0, Writers).Select(i => ThreadHelper.StartBackground($"w{i}", () =>
        {
            for (var n = 0; n < Operations; n++)
            {
                guard.Write(n);
            }
        }));

        ThreadHelper.JoinAll(TimeSpan.FromSeconds(60), readers.Concat(writers).ToArray());
        stopwatch.Stop();

        context.Output.Info($"{guard.Name,-22} {stopwatch.ElapsedMilliseconds,6} ms");
    }

    private sealed class MonitorGuard : IGuard
    {
        private readonly object _gate = new();
        private int _value;

        public string Name => "lock (Monitor)";

        public int Read() { lock (_gate) { return _value; } }

        public void Write(int value) { lock (_gate) { _value = value; } }

        public void Dispose() { }
    }

    private sealed class SemaphoreGuard : IGuard
    {
        private readonly SemaphoreSlim _gate = new(1, 1);
        private int _value;

        public string Name => "SemaphoreSlim(1,1)";

        public int Read()
        {
            _gate.Wait();
            try { return _value; }
            finally { _gate.Release(); }
        }

        public void Write(int value)
        {
            _gate.Wait();
            try { _value = value; }
            finally { _gate.Release(); }
        }

        public void Dispose() => _gate.Dispose();
    }

    private sealed class ReaderWriterGuard : IGuard
    {
        private readonly ReaderWriterLockSlim _gate = new();
        private int _value;

        public string Name => "ReaderWriterLockSlim";

        public int Read()
        {
            _gate.EnterReadLock();
            try { return _value; }
            finally { _gate.ExitReadLock(); }
        }

        public void Write(int value)
        {
            _gate.EnterWriteLock();
            try { _value = value; }
            finally { _gate.ExitWriteLock(); }
        }

        public void Dispose() => _gate.Dispose();
    }

    private sealed class SpinLockGuard : IGuard
    {
        private SpinLock _gate = new(enableThreadOwnerTracking: false);
        private int _value;

        public string Name => "SpinLock";

        public int Read()
        {
            var taken = false;
            try
            {
                _gate.Enter(ref taken);
                return _value;
            }
            finally
            {
                if (taken) _gate.Exit(useMemoryBarrier: false);
            }
        }

        public void Write(int value)
        {
            var taken = false;
            try
            {
                _gate.Enter(ref taken);
                _value = value;
            }
            finally
            {
                if (taken) _gate.Exit(useMemoryBarrier: false);
            }
        }

        public void Dispose() { }
    }

    private sealed class InterlockedGuard : IGuard
    {
        private int _value;

        public string Name => "Interlocked/Volatile";

        public int Read() => Volatile.Read(ref _value);

        public void Write(int value) => Interlocked.Exchange(ref _value, value);

        public void Dispose() { }
    }
}
