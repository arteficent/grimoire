using grimoire.Abstractions;
using grimoire.Infrastructure;

namespace grimoire.Scenarios.Deadlocks;

/// <summary>What happens when a reader needs to become a writer.</summary>
public sealed class ReaderWriterUpgradeDeadlockScenario : ScenarioBase
{
    private const int Readers = 4;

    public override string Id => "D8";

    public override string Title => "Read-to-write upgrade: exception, lost update, or upgradeable lock";

    public override string Description =>
        "Two readers both asking for the write lock is a textbook deadlock, so ReaderWriterLockSlim refuses it outright. The naive workaround then silently loses updates.";

    public override ScenarioCategory Category => ScenarioCategory.Deadlock;

    protected override string Takeaway =>
        "The BCL converts this deadlock into a LockRecursionException. Dropping the read lock to take the write lock removes the deadlock but breaks atomicity - EnterUpgradeableReadLock is the primitive designed for read-then-maybe-write, and only one thread may hold it.";

    protected override Task ExecuteAsync(IScenarioContext context, CancellationToken cancellationToken)
    {
        NaiveUpgrade(context);
        context.Output.Blank();
        var released = ReleaseThenReacquire(context);
        context.Output.Blank();
        var upgradeable = UpgradeableRead(context);

        context.Output.Blank();
        context.Output.Info($"{Readers} threads each incremented once: release-then-reacquire ended at {released}, upgradeable read ended at {upgradeable} (expected {Readers}).");

        return Task.CompletedTask;
    }

    private static void NaiveUpgrade(IScenarioContext context)
    {
        context.Output.Info("1) EnterReadLock then EnterWriteLock on the same thread");
        using var rwLock = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);

        rwLock.EnterReadLock();
        try
        {
            rwLock.EnterWriteLock();
            rwLock.ExitWriteLock();
            context.Output.Error("upgrade succeeded - this build would be deadlock-prone");
        }
        catch (LockRecursionException ex)
        {
            context.Output.Warn($"blocked by the runtime: {ex.Message}");
            context.Output.Step("Two readers doing this would each wait for the other's read lock to drop - a guaranteed deadlock.");
        }
        finally
        {
            rwLock.ExitReadLock();
        }
    }

    private static int ReleaseThenReacquire(IScenarioContext context)
    {
        context.Output.Info("2) Workaround: exit the read lock, then take the write lock (no deadlock, but not atomic)");
        using var rwLock = new ReaderWriterLockSlim();
        var value = 0;

        var threads = Enumerable.Range(1, Readers).Select(i => ThreadHelper.StartBackground($"reader-{i}", () =>
        {
            int observed;
            rwLock.EnterReadLock();
            try
            {
                observed = value;
            }
            finally
            {
                rwLock.ExitReadLock();
            }

            // The window between the two locks is where another thread's write is lost.
            Thread.Sleep(20);

            rwLock.EnterWriteLock();
            try
            {
                value = observed + 1;
            }
            finally
            {
                rwLock.ExitWriteLock();
            }
        })).ToArray();

        ThreadHelper.JoinAll(TimeSpan.FromSeconds(10), threads);
        context.Output.Error($"final value = {value} (expected {Readers}) - the read and the write were two separate critical sections.");
        return value;
    }

    private static int UpgradeableRead(IScenarioContext context)
    {
        context.Output.Info("3) EnterUpgradeableReadLock: read and conditional write stay one atomic unit");
        using var rwLock = new ReaderWriterLockSlim();
        var value = 0;

        var threads = Enumerable.Range(1, Readers).Select(i => ThreadHelper.StartBackground($"reader-{i}", () =>
        {
            rwLock.EnterUpgradeableReadLock();
            try
            {
                var observed = value;
                Thread.Sleep(20);

                rwLock.EnterWriteLock();
                try
                {
                    value = observed + 1;
                }
                finally
                {
                    rwLock.ExitWriteLock();
                }
            }
            finally
            {
                rwLock.ExitUpgradeableReadLock();
            }
        })).ToArray();

        ThreadHelper.JoinAll(TimeSpan.FromSeconds(10), threads);
        context.Output.Success($"final value = {value} (expected {Readers}) - other readers stayed unblocked while this thread decided.");
        return value;
    }
}
