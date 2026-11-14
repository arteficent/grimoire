using grimoire.Model;
using static grimoire.Model.ThreadPhase;
using static grimoire.Model.Tone;
using RK = grimoire.Model.ResourceKind;

namespace grimoire.Stories;

internal static class DeadlockStories
{
    public static IEnumerable<Story> All()
    {
        yield return LockOrdering();
        yield return LockOrderingFix();
        yield return TryEnterBackoff();
        yield return SyncOverAsync();
        yield return PoolStarvation();
        yield return Reentrancy();
        yield return MissedPulse();
        yield return ReaderWriterUpgrade();
    }

    // ------------------------------------------------------------------------------------------
    private static Story LockOrdering() => new StoryBuilder("D1",
            "Deadlock needs all four Coffman conditions: mutual exclusion, hold-and-wait, no pre-emption and circular wait. Break any one of them and it cannot happen.")
        .Setup(s =>
        {
            s.AddCores(2);
            s.AddThread("t1", "Thread 1  (A then B)");
            s.AddThread("t2", "Thread 2  (B then A)");
            s.AddResource("A", "lockA", RK.Lock, 0.1, 0.35, value: "free");
            s.AddResource("B", "lockB", RK.Lock, 0.9, 0.35, value: "free");
            s.Ram("lockA.header", "free").Ram("lockB.header", "free");
        })
        .Code("""
            var t1 = new Thread(() =>
            {
                lock (lockA)
                {
                    bothInside.Signal(); bothInside.Wait();
                    lock (lockB) { /* never reached */ }
                }
            });
            var t2 = new Thread(() =>
            {
                lock (lockB)
                {
                    bothInside.Signal(); bothInside.Wait();
                    lock (lockA) { /* never reached */ }
                }
            });
            """)
        .Step("Two threads, two locks",
            "Thread 1 needs lockA and then lockB. Thread 2 needs exactly the same two locks, but in the opposite order. Nothing looks wrong yet: each lock is just an ordinary object.",
            "The OS scheduler places the two threads on two different cores, so they really execute at the same instant - not interleaved on one core. Each lock object has a header word in a 64-byte cache line in DRAM. No core has touched those lines yet, so no cache holds them.",
            [1, 9],
            s => s.Run("t1", 0).Run("t2", 1)
                  .Note("t1", "I need A, then B")
                  .Note("t2", "I need B, then A"))
        .Step("Thread 1 takes lockA",
            "lock(lockA) calls Monitor.Enter. The lock is free, so Thread 1 becomes its owner immediately (fast path, no waiting).",
            "The fast path is one atomic instruction: lock cmpxchg swaps the header from 'free' to 'owned by T1'. An atomic write needs exclusive ownership of the cache line, so Core 0 sends a Read-For-Ownership on the bus; the line arrives in Core 0's L1 in Modified (M) state. DRAM still says 'free' - with write-back caches, the newest value only lives in Core 0's L1. Cost: about 20-100 cycles.",
            [3],
            s => s.Acquire("t1", "A").Set("A", "owner = T1")
                  .Cache(0, "lockA.hdr", "T1", 'M')
                  .Ram("lockA.header", "free (stale)", Warn)
                  .Move("ram:lockA.header", "core:0", "RFO lockA line"))
        .Step("Thread 2 takes lockB at the same moment",
            "On the other core, Thread 2 does the same with lockB. Each thread now holds one lock.",
            "Core 1 performs its own lock cmpxchg on a different cache line, so the two cores do not interfere with each other at all. This is the dangerous part: both acquisitions are fast and look perfectly healthy.",
            [11],
            s => s.Acquire("t2", "B").Set("B", "owner = T2")
                  .Cache(1, "lockB.hdr", "T2", 'M')
                  .Ram("lockB.header", "free (stale)", Warn)
                  .Move("ram:lockB.header", "core:1", "RFO lockB line"))
        .Step("Both threads reach the point of no return",
            "The demo uses a CountdownEvent so that both threads are inside their first lock before either one asks for its second lock. That makes the deadlock happen every time.",
            "In production there is no rendezvous. The window is only a few nanoseconds wide, so the code passes every test and then hangs under real load, when two requests line up exactly.",
            [5, 13],
            s => s.Note("t1", "Holding A. Now I need B.")
                  .Note("t2", "Holding B. Now I need A."))
        .Step("Each thread asks for the other's lock and spins",
            "Thread 1 calls Monitor.Enter(lockB), but T2 owns it. Thread 2 calls Monitor.Enter(lockA), but T1 owns it. Neither request can succeed.",
            "The cmpxchg fails because the owner field is not 'free'. Monitor does not sleep right away: it spins for a short, adaptive period using the PAUSE instruction (YIELD on ARM), in case the owner lets go within microseconds. While they spin, both cores keep re-reading the other core's lock line, so the line is copied between the cores in Shared state. This back-and-forth copying is why contended locks are expensive, even when they do succeed.",
            [6, 14],
            s => s.WaitOn("t1", "B", Spinning).WaitOn("t2", "A", Spinning)
                  .Cache(0, "lockB.hdr", "T2", 'S').Cache(1, "lockB.hdr", "T2", 'S')
                  .Cache(1, "lockA.hdr", "T1", 'S').Cache(0, "lockA.hdr", "T1", 'S')
                  .Reg(0, "cmpxchg failed -> PAUSE").Reg(1, "cmpxchg failed -> PAUSE")
                  .Move("core:1", "core:0", "lockB line (S)")
                  .Move("core:0", "core:1", "lockA line (S)"))
        .Step("Spinning gives up: both threads sleep in the kernel",
            "Neither lock is ever going to be released, so both threads go to sleep. The only thing that could wake them is a Monitor.Exit, and that Exit sits on code that comes after the call they are blocked in.",
            "After spinning, each thread registers itself as a waiter (Windows: kernel event / WaitOnAddress, Linux: futex) and makes a system call. A context switch saves its registers, the scheduler removes it from the run queue, and the core goes idle or runs another thread. A deadlocked process therefore uses about 0% CPU. It looks idle, not broken.",
            [6, 14],
            s => s.Phase("t1", Blocked).Phase("t2", Blocked)
                  .Unschedule("t1").Unschedule("t2")
                  .CoreNote(0, "T1 descheduled; waiting in kernel wait queue")
                  .CoreNote(1, "T2 descheduled; waiting in kernel wait queue")
                  .Say("DEADLOCK: circular wait  T1 -> lockB -> T2 -> lockA -> T1", Bad))
        .Step("Why: all four Coffman conditions hold",
            "Mutual exclusion: each lock has only one owner. Hold-and-wait: each thread keeps one lock while it asks for another. No pre-emption: the runtime cannot take a lock away from its owner. Circular wait: T1 waits for T2, and T2 waits for T1. Remove any one of these and the deadlock cannot happen. D2 removes circular wait, and D3 removes hold-and-wait.",
            "Nothing is stuck at the hardware level. Both cores are free and memory is fine. The deadlock is a purely logical cycle in the wait-for graph. That is why a debugger shows two threads inside Monitor.Enter with zero CPU time.",
            [],
            s => s.Say("Mutual exclusion + hold-and-wait + no pre-emption + circular wait", Bad))
        .Step("The watchdog gives up",
            "After 3 seconds the DeadlockWatchdog reports DEADLOCKED and abandons the threads. .NET cannot safely kill a thread (Thread.Abort no longer exists), so a real deadlock usually means restarting the process.",
            "The abandoned threads stay asleep forever. Each one still reserves its stack (1 MB by default) and keeps its kernel wait object and the lock objects alive.",
            [],
            s => s.Say("Watchdog: no progress after 3 s, reported as DEADLOCKED", Bad))
        .Build();

    // ------------------------------------------------------------------------------------------
    private static Story LockOrderingFix() => new StoryBuilder("D2",
            "A single global order over all locks makes circular wait impossible: every thread climbs the same ladder.")
        .Setup(s =>
        {
            s.AddCores(2);
            s.AddThread("t1", "worker-1  (A,B)");
            s.AddThread("t2", "worker-2  (B,A)");
            s.AddResource("A", "lockA", RK.Lock, 0.1, 0.35, caption: "rank 1", value: "free");
            s.AddResource("B", "lockB", RK.Lock, 0.9, 0.35, caption: "rank 2", value: "free");
        })
        .Code("""
            void TransferInOrder(RankedLock x, RankedLock y)
            {
                var (first, second) = x.Rank < y.Rank ? (x, y) : (y, x);
                lock (first)
                {
                    Thread.Sleep(50);          // widen the race window
                    lock (second)
                    {
                        // critical section
                    }
                }
            }

            // worker-1: TransferInOrder(lockA, lockB)
            // worker-2: TransferInOrder(lockB, lockA)
            """)
        .Step("Same two locks, now with a rank",
            "Both locks have a fixed rank. The callers still pass them in opposite orders, exactly as in D1.",
            "Two threads start on two cores. The rank is just an integer field on each lock wrapper.",
            [14, 15],
            s => s.Run("t1", 0).Run("t2", 1)
                  .Note("t1", "TransferInOrder(A, B)")
                  .Note("t2", "TransferInOrder(B, A)"))
        .Step("Sort before locking",
            "Each thread sorts its two locks by rank, so both decide to take lockA first. The order the caller used no longer matters.",
            "Sorting is a comparison of two integers in registers, which costs almost nothing. The whole fix is a rule that every code path agrees to follow.",
            [3],
            s => s.Note("t1", "first = A (rank 1)").Note("t2", "first = A (rank 1)"))
        .Step("worker-1 wins lockA; worker-2 waits while holding nothing",
            "worker-1 gets lockA. worker-2 waits for lockA, but unlike in D1 it is not holding any other lock while it waits.",
            "worker-2 spins and then sleeps on lockA's wait queue. Since it holds nothing, no wait-for edge points back at worker-1. The wait-for graph is a straight line, not a cycle, so this wait is guaranteed to end.",
            [4],
            s => s.Acquire("t1", "A").Set("A", "owner = worker-1")
                  .Cache(0, "lockA.hdr", "w1", 'M')
                  .WaitOn("t2", "A").Unschedule("t2")
                  .CoreNote(1, "worker-2 sleeping on lockA"))
        .Step("worker-1 takes lockB without contention",
            "lockB is free, as it must be: in this program nobody can hold lockB without holding lockA first.",
            "This is a second uncontended cmpxchg. The lockB cache line moves into Core 0 in M state.",
            [6, 7],
            s => s.Acquire("t1", "B").Set("B", "owner = worker-1")
                  .Cache(0, "lockB.hdr", "w1", 'M')
                  .Note("t1", "B is free - guaranteed by the order"))
        .Step("worker-1 finishes and releases both locks",
            "Leaving the lock blocks releases lockB and then lockA. Because worker-2 is waiting, the release also wakes it.",
            "Monitor.Exit writes 'free' with a release store. Because a waiter is registered, it also signals the kernel event. The OS moves worker-2 to the ready queue, and it gets a core after the wake-up latency, typically 5-50 us.",
            [9, 10, 11],
            s => s.Release("t1", "B").Release("t1", "A").Phase("t1", Done)
                  .Set("A", "free").Set("B", "free")
                  .StopWaiting("t2", "A").Run("t2", 1)
                  .Move("core:0", "core:1", "lockA line"))
        .Step("worker-2 climbs the same ladder: A, then B",
            "worker-2 takes lockA, then lockB, and does its critical section.",
            "Both cache lines now move to Core 1 in M state. The same code that deadlocked in D1 can never deadlock here.",
            [4, 7],
            s => s.Acquire("t2", "A").Acquire("t2", "B")
                  .Set("A", "owner = worker-2").Set("B", "owner = worker-2")
                  .Cache(1, "lockA.hdr", "w2", 'M').Cache(1, "lockB.hdr", "w2", 'M')
                  .Invalidate(0, "lockA.hdr").Invalidate(0, "lockB.hdr"))
        .Step("Both workers complete",
            "Both workers finish. Ordering costs throughput only while one thread waits, never liveness.",
            "In real code, derive the rank from something stable (an id or a level in a hierarchy) and assert the order in debug builds.",
            [],
            s => s.Release("t2", "B").Release("t2", "A").Phase("t2", Done)
                  .Set("A", "free").Set("B", "free")
                  .Say("No deadlock: a global lock order makes circular wait impossible", Good))
        .Build();

    // ------------------------------------------------------------------------------------------
    private static Story TryEnterBackoff() => new StoryBuilder("D3",
            "Timed acquisition breaks hold-and-wait; randomised back-off breaks symmetry. Prefer a lock order when you have one.")
        .Setup(s =>
        {
            s.AddCores(2);
            s.AddThread("t1", "worker-1  (A,B)");
            s.AddThread("t2", "worker-2  (B,A)");
            s.AddResource("A", "lockA", RK.Lock, 0.1, 0.35, value: "free");
            s.AddResource("B", "lockB", RK.Lock, 0.9, 0.35, value: "free");
        })
        .Code("""
            for (var attempt = 1; attempt <= 20; attempt++)
            {
                if (Monitor.TryEnter(first, TimeSpan.FromMilliseconds(100)))
                {
                    try
                    {
                        if (Monitor.TryEnter(second, TimeSpan.FromMilliseconds(100)))
                        {
                            try { /* critical section */ return; }
                            finally { Monitor.Exit(second); }
                        }
                    }
                    finally { Monitor.Exit(first); }
                }
                Thread.Sleep(Random.Shared.Next(20, 120));   // randomised back-off
            }
            """)
        .Step("Same trap as D1",
            "worker-1 takes lockA and worker-2 takes lockB. The locks are taken in opposite orders again.",
            "These are two uncontended cmpxchg operations on two cores, as in D1.",
            [3],
            s => s.Run("t1", 0).Run("t2", 1)
                  .Acquire("t1", "A").Acquire("t2", "B")
                  .Set("A", "owner = worker-1").Set("B", "owner = worker-2"))
        .Step("Ask for the second lock, with a deadline",
            "Each thread asks for its second lock with TryEnter(100 ms). The cycle exists, but every wait in it now has an expiry time.",
            "TryEnter spins for a while and then sleeps with a kernel timeout. The OS arms a timer for 100 ms, and the timer interrupt will forcibly end the wait, so the cycle cannot last forever.",
            [7],
            s => s.WaitOn("t1", "B", Waiting).WaitOn("t2", "A", Waiting)
                  .Unschedule("t1").Unschedule("t2")
                  .Say("Temporary cycle, but every wait has a deadline", Warn))
        .Step("Timeout: release everything you hold",
            "Both TryEnter calls return false. Each thread leaves through the finally block and releases its first lock. Hold-and-wait is broken.",
            "A hardware timer (APIC/HPET) interrupt fires, and the kernel wakes both threads with a 'timed out' status. Each thread does a release store on the lock it holds.",
            [13],
            s => s.StopWaiting("t1", "B").StopWaiting("t2", "A")
                  .Release("t1", "A").Release("t2", "B")
                  .Set("A", "free").Set("B", "free")
                  .Run("t1", 0).Run("t2", 1)
                  .Note("t1", "TryEnter(B) = false -> Exit(A)")
                  .Note("t2", "TryEnter(A) = false -> Exit(B)"))
        .Step("Randomised back-off",
            "Each thread sleeps for a random 20-120 ms before it retries.",
            "Sleep removes the thread from the run queue until its timer expires. The randomness is what breaks the symmetry. With a fixed delay, both threads would retry in lockstep and collide on every attempt. That is a livelock: the threads stay busy but never make progress.",
            [15],
            s => s.Phase("t1", Sleeping).Phase("t2", Sleeping)
                  .Unschedule("t1").Unschedule("t2")
                  .Note("t1", "sleep 35 ms").Note("t2", "sleep 92 ms"))
        .Step("worker-1 wakes first and takes both locks",
            "worker-1 wakes up earlier, takes lockA and then lockB, and enters its critical section.",
            "Both acquisitions are uncontended fast paths on Core 0.",
            [3, 7, 9],
            s => s.Run("t1", 0).Acquire("t1", "A").Acquire("t1", "B")
                  .Set("A", "owner = worker-1").Set("B", "owner = worker-1"))
        .Step("worker-2 retries and waits a short time",
            "worker-2 wakes up and tries lockB, which worker-1 holds. It waits, but only for a short, bounded time.",
            "worker-2 makes a timed kernel wait on lockB. worker-1 is about to release the lock.",
            [3],
            s => s.Run("t2", 1).WaitOn("t2", "B", Waiting))
        .Step("worker-1 releases, worker-2 gets both locks",
            "worker-1 finishes and releases both locks. worker-2's TryEnter succeeds and it takes lockB and then lockA.",
            "The release signals the waiting thread, the kernel makes worker-2 runnable, and it acquires both locks.",
            [9, 10],
            s => s.Release("t1", "B").Release("t1", "A").Phase("t1", Done)
                  .Acquire("t2", "B").Acquire("t2", "A")
                  .Set("A", "owner = worker-2").Set("B", "owner = worker-2"))
        .Step("Both workers complete",
            "2 of 2 workers completed. The retry cap (20 attempts) makes sure a pathological case fails loudly instead of retrying forever.",
            "Timeouts and random jitter are the same idea that keeps Ethernet (CSMA/CD) and distributed lock leases working.",
            [],
            s => s.Release("t2", "A").Release("t2", "B").Phase("t2", Done)
                  .Set("A", "free").Set("B", "free")
                  .Say("2/2 workers completed: timeouts plus jitter restore progress", Good))
        .Build();

    // ------------------------------------------------------------------------------------------
    private static Story SyncOverAsync() => new StoryBuilder("D4",
            "Never block on async code. Use ConfigureAwait(false) in libraries, and better still, use async all the way up the call chain.")
        .Setup(s =>
        {
            s.AddCores(2);
            s.AddThread("ui", "UI thread (pump)");
            s.AddThread("tp", "ThreadPool worker");
            s.AddResource("Q", "UI message queue", RK.Context, 0.05, 0.3);
            s.AddResource("TK", "Task<int>", RK.Task, 0.6, 0.3, caption: "LibraryCallAsync", value: "not started");
            s.AddResource("TM", "timer queue", RK.Event, 1.0, 0.3, value: "idle");
            s.Ram("SyncContext.Current", "UI context");
        })
        .Code("""
            // Runs on the UI thread (single-threaded SynchronizationContext)
            void Button_Click()
            {
                int value = LibraryCallAsync().Result;   // blocks the UI thread
                label.Text = value.ToString();
            }

            async Task<int> LibraryCallAsync()
            {
                await Task.Delay(100);                   // captures SynchronizationContext.Current
                return 42;                               // must resume on the UI thread
            }

            // Fix (library): await Task.Delay(100).ConfigureAwait(false);
            // Fix (caller):  int value = await LibraryCallAsync();
            """)
        .Step("A UI thread pumps messages",
            "A UI framework (WPF, WinForms, classic ASP.NET, or Avalonia's dispatcher) runs one special thread that processes a queue of messages. A button click arrives as one of those messages.",
            "The UI thread runs an endless loop: get a message, dispatch it, repeat. SynchronizationContext.Post adds a delegate to this queue. Only this one thread may touch the UI, so the context has exactly one consumer by design.",
            [2],
            s => s.Run("ui", 0).Go("ui", "Q").Items("Q", "Click")
                  .Note("ui", "dispatching Click"))
        .Step("The library hits 'await'",
            "LibraryCallAsync runs synchronously until its first await. Task.Delay is not finished yet, so the method saves where it was and returns an incomplete Task<int>.",
            "Task.Delay registers a timer entry, and no thread is used while it waits. The compiler-generated state machine records 'resume on the captured SynchronizationContext', which here is the UI context.",
            [10],
            s => s.Pop("Q").Go("ui", "TK")
                  .Set("TK", "pending, resumes on UI context", Info)
                  .Set("TM", "Delay(100) armed", Info)
                  .Note("ui", "await captured SynchronizationContext.Current")
                  .Move("res:TK", "res:TM", "register timer"))
        .Step(".Result blocks the UI thread",
            "Back in Button_Click, .Result waits synchronously for the task. The UI thread stops pumping messages.",
            ".Result calls Task.Wait, which makes a kernel wait on an event inside the task, and the UI thread is descheduled. Repaint, input, and timer messages pile up in its queue. Windows labels the window 'Not Responding' after about 5 seconds.",
            [4],
            s => s.WaitOn("ui", "TK").Unschedule("ui")
                  .CoreNote(0, "UI thread sleeping; nothing pumps messages")
                  .Items("Q", "WM_PAINT", "WM_MOUSEMOVE"))
        .Step("The timer fires on a pool thread",
            "After 100 ms the delay completes on a thread-pool thread. Because the await captured the UI context, the continuation ('return 42') is not run here. It is posted to the UI queue instead.",
            "The timer callback runs on a pool worker, which calls SynchronizationContext.Post. That adds the continuation to the UI queue in memory and returns. The pool thread is then free again.",
            [10, 11],
            s => s.Run("tp", 1).Go("tp", "TM")
                  .Set("TM", "fired", Good)
                  .Push("Q", "continuation: return 42")
                  .Note("tp", "Post(continuation) to UI context")
                  .Move("res:TM", "res:Q", "Post(continuation)"))
        .Step("Deadlock: one thread waits for itself",
            "The continuation is in the UI queue, and only the UI thread can run it. The UI thread runs nothing until the task completes, and the task only completes when the continuation runs. This is a cycle with just one thread in it.",
            "Both cores are idle and the continuation sits in memory. No lock is involved at all. The resource in the cycle is the single thread that the SynchronizationContext is allowed to use.",
            [4, 11],
            s =>
            {
                s.Go("tp", null, Idle).Unschedule("tp");
                s.R("TK").Owners.Add("ui");
                s.Set("TK", "only the UI thread can complete it", Bad)
                 .Say("DEADLOCK: the UI thread waits for a Task that needs the UI thread", Bad);
            })
        .Step("Fix 1: ConfigureAwait(false)",
            "With ConfigureAwait(false) the library says 'I don't need the UI context'. The continuation runs right there on the pool thread and completes the task.",
            "The timer's pool thread runs 'return 42' inline. TrySetResult stores the result, then signals the event that the UI thread is waiting on, so the kernel marks the UI thread runnable.",
            [14],
            s =>
            {
                s.R("TK").Owners.Clear();
                s.Items("Q", "WM_PAINT", "WM_MOUSEMOVE")
                 .Run("tp", 1).Go("tp", "TK")
                 .Set("TK", "RanToCompletion = 42", Good)
                 .Note("tp", "continuation runs here, on the pool")
                 .Move("thread:tp", "res:TK", "SetResult(42)");
            })
        .Step("The UI thread wakes up",
            ".Result returns 42 and the UI thread goes back to pumping messages. Blocking still froze the UI for 100 ms, though.",
            "The best fix is Fix 2: make the caller async with 'await LibraryCallAsync()'. Then the UI thread goes straight back to its loop, and the continuation is just another message it processes later.",
            [4, 5, 15],
            s => s.StopWaiting("ui", "TK").Run("ui", 0).Go("ui", "Q")
                  .Go("tp", null, Idle).Unschedule("tp")
                  .Pop("Q").Pop("Q")
                  .Say("Returned 42, no deadlock. Better: use async all the way", Good))
        .Build();

    // ------------------------------------------------------------------------------------------
    private static Story PoolStarvation() => new StoryBuilder("D5",
            "The scarce resource can be a worker thread rather than a lock. Never block a pool thread on work queued to the same pool: use await.")
        .Setup(s =>
        {
            s.AddCores(2);
            s.AddThread("w1", "worker 1");
            s.AddThread("w2", "worker 2");
            s.AddThread("m", "main thread");
            s.AddResource("P", "BoundedWorkerPool", RK.Pool, 0.0, 0.0, caption: "2 threads", value: "busy 0/2");
            s.AddResource("Q", "work queue", RK.Queue, 1.0, 0.0, value: "empty");
            s.AddResource("I1", "inner #1", RK.Task, 0.2, 1.0, value: "-");
            s.AddResource("I2", "inner #2", RK.Task, 0.6, 1.0, value: "-");
        })
        .Code("""
            var pool = new BoundedWorkerPool(workers: 2);

            for (var i = 1; i <= 2; i++)
            {
                pool.Enqueue(() =>
                {
                    var inner = pool.Enqueue(() => Compute());   // needs a free worker
                    inner.Wait();                                // blocks this worker
                });
            }
            """)
        .Step("Two workers wait for work",
            "The pool has exactly two worker threads. Both are blocked in BlockingCollection.Take(), waiting for items.",
            "The workers are asleep in the kernel and use no CPU. A pool's capacity is its number of threads, not its number of cores.",
            [1],
            s => s.WaitOn("w1", "Q", Waiting).WaitOn("w2", "Q", Waiting).Run("m", 0))
        .Step("Main enqueues two outer items",
            "Two outer work items go into the queue.",
            "Add stores the delegates in the collection and signals its semaphore, which wakes the sleeping workers.",
            [5],
            s => s.Items("Q", "outer 1", "outer 2").Set("Q", null)
                  .Move("thread:m", "res:Q", "Enqueue x2"))
        .Step("Each worker picks up an outer item",
            "worker 1 runs outer 1 and worker 2 runs outer 2. All of the pool's capacity is now in use.",
            "Both workers become runnable and get the two cores.",
            [5, 6],
            s => s.Pop("Q").Pop("Q")
                  .StopWaiting("w1", "Q").StopWaiting("w2", "Q")
                  .Acquire("w1", "P").Acquire("w2", "P")
                  .Run("w1", 0).Run("w2", 1).Phase("m", Idle).Unschedule("m")
                  .Set("P", "busy 2/2", Warn)
                  .Note("w1", "running outer 1").Note("w2", "running outer 2"))
        .Step("Each outer item enqueues an inner item",
            "Each outer item needs a sub-result, so it puts an inner item into the same pool.",
            "The inner items are added to the queue memory. There is no free worker to take them.",
            [7],
            s => s.Items("Q", "inner 1", "inner 2")
                  .Set("I1", "queued, not started").Set("I2", "queued, not started")
                  .Move("thread:w1", "res:Q", "Enqueue(inner 1)")
                  .Move("thread:w2", "res:Q", "Enqueue(inner 2)"))
        .Step("...and then blocks waiting for it",
            "inner.Wait() blocks the worker, which keeps its slot in the pool while it sleeps. The inner items can only run on a free worker, and there will never be one.",
            "Both cores are idle and CPU is at 0%, yet the pool is 'full'. The item in the cycle is not a lock but a worker thread: each worker holds one and waits for work that needs another.",
            [8],
            s => s.WaitOn("w1", "I1").WaitOn("w2", "I2")
                  .Unschedule("w1").Unschedule("w2")
                  .Set("I1", "needs a free worker", Bad).Set("I2", "needs a free worker", Bad)
                  .Set("P", "busy 2/2, both blocked", Bad)
                  .Deadlock("w1", "w2")
                  .Say("STARVATION DEADLOCK: queued work needs a worker, and every worker waits for queued work", Bad))
        .Step("What the real .NET ThreadPool does",
            "The CLR thread pool has a starvation detector. If queued work makes no progress, it adds roughly one extra thread every 0.5-1 s (the hill-climbing algorithm). The same code therefore 'works' in ASP.NET, but with seconds of latency, which is the classic thread-pool starvation outage. A bounded pool like this one never recovers.",
            "Each extra thread costs a 1 MB stack reservation, a kernel thread object, and more work for the scheduler. During a burst of requests, the injection rate cannot keep up with new blocking calls.",
            [8],
            s => s.Set("I1", "needs a free worker", Bad).Set("I2", "needs a free worker", Bad)
                  .Say("Real ThreadPool: slowly adds threads, so you get latency spikes rather than a fix", Warn))
        .Step("Fix: don't block a worker on its own pool",
            "Either run the inner work inline, or use 'await inner' so the worker returns to the pool while it waits.",
            "With await, the outer item's state machine is stored on the heap and the thread goes back to the queue. The inner items now find free workers.",
            [7, 8],
            s => s.StopWaiting("w1", "I1").StopWaiting("w2", "I2").ClearDeadlock()
                  .Items("Q").Set("Q", "empty")
                  .Run("w1", 0).Run("w2", 1).Go("w1", "I1").Go("w2", "I2")
                  .Set("I1", "completed", Good).Set("I2", "completed", Good)
                  .Note("w1", "await inner -> thread freed while waiting"))
        .Step("All items complete",
            "Every outer and inner item has finished.",
            "Make sure the thing you wait for never needs the thing you are holding, whether that is a lock, a thread, or a database connection.",
            [],
            s => s.Release("w1", "P").Release("w2", "P")
                  .Phase("w1", Done).Phase("w2", Done)
                  .Set("P", "busy 0/2", Good)
                  .Say("Completed: async waits release the worker instead of holding it", Good))
        .Build();

    // ------------------------------------------------------------------------------------------
    private static Story Reentrancy() => new StoryBuilder("D6",
            "Know your primitive's reentrancy. Separate the public API (which takes the lock) from private helpers (which assume it is held).")
        .Setup(s =>
        {
            s.AddCores(1);
            s.AddThread("t1", "Thread 1");
            s.AddResource("G", "gate", RK.Lock, 0.1, 0.4, value: "free");
            s.AddResource("S", "SemaphoreSlim(1,1)", RK.Semaphore, 0.9, 0.4, value: "count = 1");
            s.Ram("gate.header", "free").Ram("sem._currentCount", "1");
        })
        .Code("""
            lock (gate)                  // owner = T1, recursion = 1
            {
                lock (gate) { }          // same owner -> recursion = 2, fine
            }

            semaphore.Wait();            // count 1 -> 0
            semaphore.Wait();            // count 0 -> blocks: no owner concept

            public void Do()
            {
                semaphore.Wait();
                try { DoCore(); } finally { semaphore.Release(); }
            }
            private void DoCore() { /* assumes the semaphore is already held */ }
            """)
        .Step("Monitor records who owns it",
            "lock(gate) succeeds. The lock records which thread owns it.",
            "The object header (or a 'sync block' once the lock has been contended) stores the owning managed thread id and a recursion counter. The line is pulled into Core 0 in M state.",
            [1],
            s => s.Run("t1", 0).Acquire("t1", "G").Set("G", "owner = T1, recursion = 1")
                  .Cache(0, "gate.hdr", "T1 / 1", 'M')
                  .Move("ram:gate.header", "core:0", "RFO"))
        .Step("Re-entering the same lock",
            "The same thread locks gate again, for example through a nested call. Monitor sees that the owner is the current thread and simply increments a counter.",
            "Re-entry compares the stored owner id with the current thread id and increments the counter. This is a plain write to a line Core 0 already owns, with no bus traffic and no kernel call.",
            [3],
            s => s.Set("G", "owner = T1, recursion = 2", Good)
                  .Cache(0, "gate.hdr", "T1 / 2", 'M')
                  .Note("t1", "owner == me, so just count++"))
        .Step("Exit twice and the lock is free",
            "Each Exit decrements the counter. When it reaches 0, the lock is released.",
            "The final exit is a release store of 'free'. If any waiters were registered, it would also signal them.",
            [3, 4],
            s => s.Release("t1", "G").Set("G", "free", Neutral)
                  .Cache(0, "gate.hdr", "free", 'M'))
        .Step("SemaphoreSlim is just a counter",
            "semaphore.Wait() decrements the count from 1 to 0. It does not remember who decremented it.",
            "Wait does an Interlocked compare-exchange on _currentCount while it is greater than 0. Any thread may call Release, which is useful for async code, but it means the semaphore has no concept of an owner and so cannot support recursion.",
            [6],
            s => s.Acquire("t1", "S").Set("S", "count = 0 (owner not recorded)")
                  .Cache(0, "sem.count", "0", 'M')
                  .Move("ram:sem._currentCount", "core:0", "RFO"))
        .Step("The second Wait blocks on itself",
            "The same thread calls Wait() again. The count is 0, so it waits for a Release that only it could perform, and it will never get there.",
            "The thread spins briefly, then sleeps on the semaphore's kernel event. The only code that could call Release is further down this same thread's stack, which will never run.",
            [7],
            s => s.WaitOn("t1", "S").Unschedule("t1")
                  .Set("S", "count = 0", Bad)
                  .CoreNote(0, "T1 sleeping, waiting for itself")
                  .Say("SELF-DEADLOCK: Thread 1 waits for a permit it already holds", Bad))
        .Step("Fix: split the API",
            "The public method takes the semaphore once. Private helpers assume it is already held and never take it again.",
            "There is now exactly one Wait/Release pair per call path. A debug-only assertion (for example, a counter checked with Debug.Assert) can catch accidental re-entry.",
            [9, 10, 11, 12, 13, 14],
            s => s.StopWaiting("t1", "S").Run("t1", 0).Acquire("t1", "S")
                  .Set("S", "count = 0 (held once)", Good)
                  .Note("t1", "DoCore() never re-acquires"))
        .Step("Release",
            "Release puts the count back to 1 and the call completes.",
            "Monitor is reentrant. SemaphoreSlim, Mutex-like async locks, and SpinLock (which throws if you try) are not.",
            [12],
            s => s.Release("t1", "S").Phase("t1", Done).Set("S", "count = 1", Neutral)
                  .Say("Monitor is reentrant; SemaphoreSlim and async locks are not", Good))
        .Build();

    // ------------------------------------------------------------------------------------------
    private static Story MissedPulse() => new StoryBuilder("D7",
            "Always wait in a loop over the condition: while (!predicate) Monitor.Wait(gate). Signals are events; state is what you must check.")
        .Setup(s =>
        {
            s.AddCores(2);
            s.AddThread("p", "producer");
            s.AddThread("c", "consumer");
            s.AddResource("G", "gate", RK.Lock, 0.0, 0.35, value: "free");
            s.AddResource("WQ", "gate wait-queue", RK.Event, 0.5, 0.35, value: "empty");
            s.AddResource("HD", "hasData", RK.Variable, 1.0, 0.35, value: "false");
            s.Ram("hasData", "false");
        })
        .Code("""
            // producer (runs first)
            lock (gate)
            {
                hasData = true;
                Monitor.Pulse(gate);      // wakes ONE waiter... if there is one
            }

            // consumer: buggy
            lock (gate)
            {
                Monitor.Wait(gate);       // waits for a pulse that already happened
            }

            // consumer: fixed
            lock (gate)
            {
                while (!hasData)          // check the state, not the signal
                    Monitor.Wait(gate);
            }
            """)
        .Step("The producer takes the lock",
            "The producer runs first, which happens all the time with real scheduling.",
            "This is an uncontended cmpxchg on gate's header from Core 0.",
            [2],
            s => s.Run("p", 0).Acquire("p", "G").Set("G", "owner = producer"))
        .Step("It sets hasData = true",
            "The shared state now says data is ready.",
            "The store goes into Core 0's store buffer and then into its L1 in M state. DRAM is stale, which is normal for write-back caches.",
            [4],
            s => s.Go("p", "HD").Set("HD", "true", Good)
                  .Cache(0, "hasData", "true", 'M')
                  .Ram("hasData", "false (stale)", Warn))
        .Step("Pulse with nobody listening",
            "Monitor.Pulse moves one waiting thread to the ready queue, but there are no waiters. The pulse disappears: no flag or counter in memory remembers it.",
            "Each Monitor has a ready queue (threads waiting to acquire the lock) and a wait queue (threads that called Wait). Pulse checks the wait queue, finds it empty, and does nothing. Unlike ManualResetEvent, it is not sticky.",
            [5],
            s => s.Go("p", "WQ").Set("WQ", "empty, so the pulse is discarded", Warn)
                  .Note("p", "Pulse()... nobody is waiting"))
        .Step("The producer leaves",
            "The producer releases gate and finishes.",
            "The release store makes the lock line free.",
            [6],
            s => s.Release("p", "G").Phase("p", Done).Set("G", "free").Set("WQ", "empty", Neutral))
        .Step("The consumer arrives late and takes the lock",
            "100 ms later the consumer locks gate.",
            "Core 1 requests the gate and hasData lines. Coherence fetches the new value from Core 0's cache, so hasData = true is right there in Core 1's L1.",
            [9],
            s => s.Run("c", 1).Acquire("c", "G").Set("G", "owner = consumer")
                  .Cache(1, "hasData", "true", 'S').Cache(0, "hasData", "true", 'S')
                  .Move("core:0", "core:1", "hasData line"))
        .Step("Buggy: Wait without checking",
            "The consumer calls Monitor.Wait without looking at hasData. Wait releases the lock and sleeps until a pulse that will never come.",
            "Monitor.Wait atomically releases gate and parks the thread on the condition's wait queue. hasData = true is in Core 1's cache, but the code never reads it, and no one will Pulse again.",
            [11],
            s => s.Release("c", "G").Set("G", "free")
                  .WaitOn("c", "WQ").Unschedule("c")
                  .Set("WQ", "consumer waiting forever", Bad)
                  .Deadlock("c")
                  .Say("LOST WAKE-UP: the pulse came before the wait, so the consumer sleeps forever", Bad))
        .Step("Fixed: check the condition first",
            "With 'while (!hasData) Wait()', the consumer looks at the shared state. hasData is already true, so it never waits.",
            "One load from a line that is already cached costs about 1 ns.",
            [17],
            s => s.StopWaiting("c", "WQ").ClearDeadlock().Set("WQ", "empty", Neutral)
                  .Run("c", 1).Acquire("c", "G").Set("G", "owner = consumer")
                  .Go("c", "HD").Note("c", "hasData == true, so skip Wait"))
        .Step("The consumer proceeds",
            "The data is consumed and the lock is released.",
            "The loop also handles spurious wake-ups and the case where another consumer takes the item between Pulse and re-acquiring the lock.",
            [17, 18],
            s => s.Release("c", "G").Phase("c", Done).Set("G", "free")
                  .Say("Wait in a loop over the condition; it handles early and spurious signals", Good))
        .Build();

    // ------------------------------------------------------------------------------------------
    private static Story ReaderWriterUpgrade() => new StoryBuilder("D8",
            "EnterUpgradeableReadLock is the primitive for read-then-maybe-write. Only one thread may hold it, and that rule is what prevents the upgrade deadlock.")
        .Setup(s =>
        {
            s.AddCores(2);
            s.AddThread("r1", "thread 1");
            s.AddThread("r2", "thread 2");
            s.AddThread("r3", "thread 3");
            s.AddThread("r4", "thread 4");
            s.AddResource("RW", "ReaderWriterLockSlim", RK.ReaderWriter, 0.15, 0.35, value: "free");
            s.AddResource("V", "value", RK.Variable, 0.95, 0.35, value: "0");
            s.Ram("value", "0");
        })
        .Code("""
            // 1. naive upgrade: refused by the runtime
            rw.EnterReadLock();
            rw.EnterWriteLock();          // LockRecursionException

            // 2. exit-then-reacquire: race window
            rw.EnterReadLock();  var seen = value;  rw.ExitReadLock();
            Thread.Sleep(200);            // another thread writes here
            rw.EnterWriteLock(); value = seen + 1;  rw.ExitWriteLock();

            // 3. upgradeable read: only one at a time
            rw.EnterUpgradeableReadLock();
            var current = value;
            rw.EnterWriteLock(); value = current + 1; rw.ExitWriteLock();
            rw.ExitUpgradeableReadLock();
            """)
        .Step("Two readers share the lock",
            "Threads 1 and 2 both hold the read lock at the same time, which is the point of a reader/writer lock.",
            "EnterReadLock atomically increments a reader count. Both cores update the same cache line, so it moves between them on every enter and exit.",
            [2],
            s => s.Run("r1", 0).Run("r2", 1)
                  .Acquire("r1", "RW").Acquire("r2", "RW").Set("RW", "readers = 2"))
        .Step("If both asked to upgrade...",
            "Each reader now wants to write. A writer must wait until all readers have left, including the other reader that is also trying to upgrade. Each waits for the other forever.",
            "This is the textbook upgrade deadlock: two readers each hold a share and each waits for exclusive access.",
            [3],
            s => s.WaitOn("r1", "RW").WaitOn("r2", "RW")
                  .Note("r1", "write? wait for readers = 0").Note("r2", "write? wait for readers = 0")
                  .Say("Each upgrader waits for the other reader to leave", Bad))
        .Step("The runtime refuses: LockRecursionException",
            "ReaderWriterLockSlim detects that you already hold a read lock and throws instead of deadlocking.",
            "This is a cheap check of per-thread lock state that turns a silent hang into a loud failure.",
            [3],
            s => s.StopWaiting("r1", "RW").StopWaiting("r2", "RW")
                  .Release("r1", "RW").Release("r2", "RW").Set("RW", "free")
                  .Phase("r1", Faulted).Phase("r2", Idle).Unschedule("r2")
                  .Note("r1", "throws LockRecursionException")
                  .Say("ReaderWriterLockSlim throws rather than deadlocking", Warn))
        .Step("Workaround: read, exit, then write",
            "Four threads each read value = 0 under the read lock.",
            "Each thread copies 0 into a register or stack slot named 'seen'.",
            [6],
            s => s.Run("r1", 0).Run("r2", 1).Phase("r3", Running).Phase("r4", Running)
                  .Acquire("r1", "RW").Acquire("r2", "RW").Acquire("r3", "RW").Acquire("r4", "RW")
                  .Set("RW", "readers = 4")
                  .Reg(0, "seen = 0").Reg(1, "seen = 0")
                  .Cache(0, "value", "0", 'S').Cache(1, "value", "0", 'S'))
        .Step("The race window",
            "Every thread exits the read lock and is about to take the write lock. During this gap the lock protects nothing, and every 'seen' is now out of date.",
            "Those private copies of 'seen' are no longer connected to the shared value. Coherence keeps caches in sync, but not registers or local variables.",
            [6, 7],
            s => s.Release("r1", "RW").Release("r2", "RW").Release("r3", "RW").Release("r4", "RW")
                  .Set("RW", "free")
                  .Phase("r1", Sleeping).Phase("r2", Sleeping).Phase("r3", Sleeping).Phase("r4", Sleeping)
                  .Unschedule("r1").Unschedule("r2")
                  .Note("r1", "my 'seen = 0' is now stale"))
        .Step("Writers take turns, using stale data",
            "Each thread takes the write lock in turn and writes seen + 1. Each one writes 1.",
            "The write lock serialises the stores correctly. The problem is that the values being stored were read earlier, outside the lock.",
            [8],
            s => s.Run("r1", 0).Acquire("r1", "RW").Set("RW", "writer = thread 1")
                  .WaitOn("r2", "RW").WaitOn("r3", "RW").WaitOn("r4", "RW")
                  .Set("V", "1").Cache(0, "value", "1", 'M').Invalidate(1, "value")
                  .Note("r1", "value = 0 + 1"))
        .Step("Lost updates",
            "Four increments ran, but the value is 1. Three updates were silently overwritten.",
            "There was no deadlock and no exception. This is the most dangerous kind of bug, because nothing tells you it happened.",
            [8],
            s => s.Release("r1", "RW")
                  .StopWaiting("r2", "RW").StopWaiting("r3", "RW").StopWaiting("r4", "RW")
                  .Phase("r1", Done).Phase("r2", Done).Phase("r3", Done).Phase("r4", Done)
                  .Set("RW", "free").Set("V", "1 (expected 4)", Bad)
                  .Say("3 of 4 increments lost: each thread wrote seen + 1 = 1", Bad))
        .Step("Fix: EnterUpgradeableReadLock",
            "Upgradeable mode allows only one holder at a time, and plain readers can still read alongside it. That holder can safely upgrade to write.",
            "With only one possible upgrader, the 'two upgraders' cycle cannot form. The other threads queue for upgradeable mode instead of reading stale data.",
            [11, 12, 13],
            s => s.Set("V", "0", Neutral).Ram("value", "0")
                  .Go("r1", null, Idle).Go("r2", null, Idle).Go("r3", null, Idle).Go("r4", null, Idle)
                  .Run("r1", 0).Acquire("r1", "RW").Set("RW", "upgradeable: thread 1")
                  .WaitOn("r2", "RW").WaitOn("r3", "RW").WaitOn("r4", "RW")
                  .Set("V", "1").Note("r1", "read 0, write 1 while still holding the lock"))
        .Step("Read-modify-write, one thread at a time",
            "Each thread reads and writes while holding upgradeable mode. The final value is 4.",
            "Use this pattern for 'check a cache, populate it if missing' code. If you never write, plain read locks scale better.",
            [11, 12, 13, 14],
            s => s.Release("r1", "RW")
                  .StopWaiting("r2", "RW").StopWaiting("r3", "RW").StopWaiting("r4", "RW")
                  .Phase("r1", Done).Phase("r2", Done).Phase("r3", Done).Phase("r4", Done)
                  .Set("RW", "free").Set("V", "4", Good)
                  .Say("value = 4: no lost updates and no deadlock", Good))
        .Build();
}
