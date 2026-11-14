namespace grimoire.Infrastructure;

public static class ThreadHelper
{
    public static Thread StartBackground(string name, Action body)
    {
        var thread = new Thread(() =>
        {
            try
            {
                body();
            }
            catch
            {
                // Deadlocked demo threads are deliberately abandoned; a late ObjectDisposedException
                // on a torn-down primitive must not take the whole process down.
            }
        })
        {
            IsBackground = true,
            Name = name
        };

        thread.Start();
        return thread;
    }

    /// <summary>Joins every thread, returning false if any of them was still stuck.</summary>
    public static bool JoinAll(TimeSpan timeout, params Thread[] threads)
    {
        var deadline = DateTime.UtcNow + timeout;
        var allJoined = true;

        foreach (var thread in threads)
        {
            var remaining = deadline - DateTime.UtcNow;
            allJoined &= remaining > TimeSpan.Zero && thread.Join(remaining);
        }

        return allJoined;
    }
}
