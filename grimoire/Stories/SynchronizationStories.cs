using grimoire.Model;
using static grimoire.Model.ThreadPhase;
using static grimoire.Model.Tone;
using RK = grimoire.Model.ResourceKind;

namespace grimoire.Stories;

internal static class SynchronizationStories
{
    public static IEnumerable<Story> All()
    {
        yield return LostUpdate();
        yield return PrimitiveTour();
        yield return Coordination();
        yield return MemoryVisibility();
        yield return AsyncMutex();
    }

    // ------------------------------------------------------------------------------------------
    private static Story LostUpdate() => new StoryBuilder("S1",
            "Interlocked is the cheapest correct option for a single word. Use lock when the invariant spans more than one field.")
        .Setup(s =>
        {
            s.AddCores(2);
            s.AddThread("t1", "Thread 1");
            s.AddThread("t2", "Thread 2");
            s.AddResource("C", "counter", RK.Variable, 0.75, 0.4, value: "41");
            s.AddResource("G", "gate", RK.Lock, 0.05, 0.4, value: "free");
            s.Ram("counter", "41");
        })
        .Code("""
            // counter++ is really three machine steps:
            //   mov eax, [counter]    ; load
            //   inc eax               ; add (in a private register)
            //   mov [counter], eax    ; store
            for (var i = 0; i < 100_000; i++) counter++;

            for (var i = 0; i < 100_000; i++) Interlocked.Increment(ref counter);   // lock xadd

            for (var i = 0; i < 100_000; i++) lock (gate) counter++;
            """)
        .Step("counter++ is not one operation",
            "The real demo runs 8 threads x 100,000 increments. Here we zoom in on two threads and a single increment each, starting from 41.",
            "The JIT emits a load, an add and a store. Even a single 'add [mem], 1' instruction is split into micro-ops (load, add, store) and is not atomic without the LOCK prefix.",
            [1, 2, 3, 4, 5],
            s => s.Run("t1", 0).Run("t2", 1).Go("t1", "C").Go("t2", "C")
                  .Note("t1", "counter++").Note("t2", "counter++"))
        .Step("Both cores load 41",
            "Both threads read the counter at nearly the same time. Both see 41.",
            "Each core takes a cache miss and fetches the line in Shared state. Many cores can hold an S copy of a line at once, so nothing is wrong yet.",
            [2],
            s => s.Cache(0, "counter", "41", 'S').Cache(1, "counter", "41", 'S')
                  .Reg(0, "eax = 41").Reg(1, "eax = 41")
                  .Move("ram:counter", "core:0", "load 41")
                  .Move("ram:counter", "core:1", "load 41"))
        .Step("Both increment in private registers",
            "Each core computes 41 + 1 = 42 in its own register.",
            "Registers belong to a single core. Neither core can see the other's eax, so each acts as if it were the only one.",
            [3],
            s => s.Reg(0, "eax = 42").Reg(1, "eax = 42"))
        .Step("Core 0 stores 42",
            "Thread 1 writes 42.",
            "To write, Core 0 needs exclusive ownership. It sends an invalidate and Core 1's copy drops to I. Coherence works perfectly here, but Core 1 had already read the old value into eax, and coherence does not track registers.",
            [4],
            s => s.Cache(0, "counter", "42", 'M').Invalidate(1, "counter")
                  .Set("C", "42")
                  .Ram("counter", "41 (stale)", Warn)
                  .Move("core:0", "core:1", "invalidate", Warn))
        .Step("Core 1 stores 42 too",
            "Thread 2 also writes 42. Two increments ran, but the counter only went up by one.",
            "Core 1 fetches the line (now 42) from Core 0 and overwrites it with its own 42, which was computed from the old value. One increment is gone. Over 800,000 increments, the real demo typically loses tens of thousands.",
            [4],
            s => s.Cache(1, "counter", "42", 'M').Invalidate(0, "counter")
                  .Set("C", "42 (expected 43)", Bad)
                  .Move("core:0", "core:1", "line fetch for write")
                  .Say("LOST UPDATE: two increments, but the counter only grew by 1", Bad))
        .Step("Fix 1: Interlocked.Increment, a single lock xadd",
            "Start again at 41. Interlocked.Increment does the load, add and store as one indivisible operation.",
            "The LOCK prefix makes Core 0 hold the line in M state and refuse requests from other cores until the read-modify-write is done. This is a 'cache lock'; the whole bus is not locked. Core 1's request just stalls for a few dozen cycles. No thread ever sleeps, so this is lock-free.",
            [7],
            s => s.Set("C", "42", Neutral).Ram("counter", "41")
                  .Cache(0, "counter", "42", 'M').Invalidate(1, "counter")
                  .Reg(0, "lock xadd -> 42").Reg(1, "lock xadd: stalled, line held by core 0")
                  .Note("t1", "lock xadd [counter], 1"))
        .Step("Core 1's xadd then sees 42",
            "Thread 2's atomic increment runs next and reads the value that was actually committed.",
            "The line moves to Core 1, which adds atomically and gets 43. The increments are serialised by the cache-coherence protocol itself.",
            [7],
            s => s.Cache(1, "counter", "43", 'M').Invalidate(0, "counter")
                  .Reg(0, null).Reg(1, "lock xadd -> 43")
                  .Set("C", "43", Good)
                  .Move("core:0", "core:1", "line (42)")
                  .Say("Correct: 43. Interlocked is one atomic read-modify-write on one cache line", Good))
        .Step("Fix 2: lock, when the invariant spans several fields",
            "lock(gate) also gives the correct result. Use it when an update touches more than one field (for example, total and count together).",
            "Monitor costs one atomic operation to enter and a release store to exit, plus a context switch if there is contention. That is slower than Interlocked for a single counter, but it can protect any amount of state. The real demo: ++ loses updates, while Interlocked and lock both reach 800,000.",
            [9],
            s => s.Reg(1, null).Set("C", "43", Neutral)
                  .Acquire("t1", "G").Set("G", "owner = Thread 1")
                  .WaitOn("t2", "G").Unschedule("t2")
                  .Go("t1", "G")
                  .Say("++ loses updates; Interlocked and lock both reach 800,000", Good))
        .Build();

    // ------------------------------------------------------------------------------------------
    private static Story PrimitiveTour() => new StoryBuilder("S2",
            "Mutual exclusion is the wrong default for read-heavy state. Measure your own workload, and use ReaderWriterLockSlim, Interlocked or immutable snapshots where reads dominate.")
        .Setup(s =>
        {
            s.AddCores(4);
            s.AddThread("r1", "reader 1");
            s.AddThread("r2", "reader 2");
            s.AddThread("r3", "reader 3");
            s.AddThread("w1", "writer");
            s.AddResource("G", "guard", RK.Lock, 0.5, 0.45, caption: "6 readers / 2 writers", value: "-");
            s.Ram("_value", "7");
        })
        .Code("""
            lock (_gate) { return _value; }                                  // Monitor

            _sem.Wait(); try { return _value; } finally { _sem.Release(); }  // SemaphoreSlim

            _rw.EnterReadLock(); try { return _value; }                      // ReaderWriterLockSlim
            finally { _rw.ExitReadLock(); }

            var taken = false; _spin.Enter(ref taken);                       // SpinLock
            try { return _value; } finally { _spin.Exit(); }

            return Volatile.Read(ref _value);                                // lock-free read
            Interlocked.Exchange(ref _value, v);                             // lock-free write
            """)
        .Step("The workload: mostly reads",
            "The real demo runs 6 reader threads and 2 writer threads, each doing 50,000 operations on one shared integer, using each primitive in turn.",
            "Four threads run on four cores. The shared value lives in a single cache line, and how that line moves between cores decides the cost of each primitive.",
            [],
            s => s.Run("r1", 0).Run("r2", 1).Run("r3", 2).Run("w1", 3))
        .Step("Monitor (lock)",
            "Only one thread at a time, even though readers do not conflict with each other.",
            "Readers are serialised for no reason. Waiters spin and then sleep, and each hand-off costs a kernel wake-up (microseconds). The lock-word line moves to every new owner.",
            [1],
            s => s.Acquire("r1", "G").WaitOn("r2", "G").WaitOn("r3", "G").WaitOn("w1", "G")
                  .Unschedule("r2").Unschedule("r3").Unschedule("w1")
                  .Set("G", "1 thread at a time")
                  .Cache(0, "lockword", "r1", 'M'))
        .Step("SemaphoreSlim",
            "The same one-at-a-time behaviour, with slightly more bookkeeping. Its strength is WaitAsync (see S5), not raw speed.",
            "The count is changed with Interlocked, and waiters are kept in a managed list plus a kernel event once they need to sleep.",
            [3],
            s =>
            {
                s.R("G").Kind = RK.Semaphore;
                s.R("G").Label = "SemaphoreSlim(1,1)";
                s.Set("G", "count = 0, 3 waiters");
            })
        .Step("ReaderWriterLockSlim",
            "Readers now run in parallel. The writer waits until the readers have left.",
            "Each EnterReadLock still does an atomic update of a shared reader count, so that cache line still moves between cores on every enter and exit. Reads scale, but not perfectly.",
            [5, 6],
            s =>
            {
                s.R("G").Kind = RK.ReaderWriter;
                s.R("G").Label = "ReaderWriterLockSlim";
                s.StopWaiting("r2", "G").StopWaiting("r3", "G")
                 .Acquire("r2", "G").Acquire("r3", "G")
                 .Run("r2", 1).Run("r3", 2)
                 .Set("G", "readers = 3, writer waiting")
                 .Cache(0, "rwcount", "3", 'S').Cache(1, "rwcount", "3", 'S').Cache(2, "rwcount", "3", 'M')
                 .Move("core:1", "core:2", "rwcount line");
            })
        .Step("SpinLock",
            "Waiters never sleep. They burn their own cores in a loop.",
            "This is great when the critical section is a few tens of nanoseconds and there are no more threads than cores. It is terrible if the holder is pre-empted, because everyone else spins for a whole time slice (about 15 ms on Windows). Each spinner hammers the lock line, which causes coherence traffic.",
            [8, 9],
            s =>
            {
                s.R("G").Kind = RK.Lock;
                s.R("G").Label = "SpinLock";
                s.Release("r2", "G").Release("r3", "G")
                 .WaitOn("r2", "G", Spinning).WaitOn("r3", "G", Spinning).WaitOn("w1", "G", Spinning)
                 .Run("r2", 1).Run("r3", 2).Run("w1", 3)
                 .Set("G", "busy-wait, 100% CPU on 3 cores", Warn)
                 .Reg(1, "PAUSE loop").Reg(2, "PAUSE loop").Reg(3, "PAUSE loop");
            })
        .Step("Interlocked / Volatile: no lock at all",
            "Readers do a plain read, and writers do an atomic exchange. Nobody waits.",
            "On x86, Volatile.Read is just a mov plus a compiler barrier; on ARM64 it is ldar. The line sits in Shared state in every reader's cache, so reads scale linearly. Only a writer's xchg invalidates the other copies.",
            [11, 12],
            s =>
            {
                s.R("G").Kind = RK.Variable;
                s.R("G").Label = "_value";
                s.Release("r1", "G");
                foreach (var t in new[] { "r1", "r2", "r3", "w1" })
                {
                    s.StopWaiting(t, "G").Go(t, "G", Running);
                }

                s.Set("G", "lock-free", Good)
                 .Reg(1, null).Reg(2, null).Reg(3, "xchg [_value], v")
                 .Cache(0, "_value", "7", 'S').Cache(1, "_value", "7", 'S').Cache(2, "_value", "7", 'S')
                 .Say("Use 'Run the real code' to time all five on your machine", Info);
            })
        .Build();

    // ------------------------------------------------------------------------------------------
    private static Story Coordination() => new StoryBuilder("S3",
            "Events are sticky (Set stays set until Reset). A Barrier counts participants, so if one of them leaves early, the others wait forever.")
        .Setup(s =>
        {
            s.AddCores(4);
            s.AddThread("w0", "worker 0");
            s.AddThread("w1", "worker 1");
            s.AddThread("w2", "worker 2");
            s.AddThread("w3", "worker 3");
            s.AddThread("m", "main");
            s.AddResource("SG", "startGate", RK.Event, 0.0, 0.25, caption: "ManualResetEventSlim", value: "unset");
            s.AddResource("B", "barrier", RK.Barrier, 0.5, 0.25, caption: "Barrier(4)", value: "phase 0, 0/4");
            s.AddResource("F", "finished", RK.Event, 1.0, 0.25, caption: "CountdownEvent(4)", value: "count = 4");
        })
        .Code("""
            var startGate = new ManualResetEventSlim(false);
            var barrier   = new Barrier(4, _ => Log("phase complete"));
            var finished  = new CountdownEvent(4);

            // worker i
            startGate.Wait();
            Work(30 * i);  barrier.SignalAndWait();   // phase 1
            Work(30 * i);  barrier.SignalAndWait();   // phase 2
            finished.Signal();

            // main
            startGate.Set();
            finished.Wait();
            """)
        .Step("Workers wait at the start gate",
            "Four workers start and immediately block on startGate.Wait(), so they can all be released at the same moment.",
            "ManualResetEventSlim spins briefly and then sleeps the thread on a kernel event. All four are off the CPU.",
            [6],
            s => s.WaitOn("w0", "SG", Waiting).WaitOn("w1", "SG", Waiting)
                  .WaitOn("w2", "SG", Waiting).WaitOn("w3", "SG", Waiting)
                  .Run("m", 0))
        .Step("Main opens the gate",
            "startGate.Set() releases every waiter, and it stays set. A worker arriving later would pass straight through.",
            "Set flips a flag and wakes all waiters at once (a 'thundering herd'). The kernel makes four threads runnable and the scheduler spreads them across four cores.",
            [12],
            s =>
            {
                foreach (var w in new[] { "w0", "w1", "w2", "w3" })
                {
                    s.StopWaiting(w, "SG");
                }

                s.Set("SG", "SET (stays set)", Good)
                 .Run("w0", 0).Run("w1", 1).Run("w2", 2).Run("w3", 3)
                 .Phase("m", Waiting).Go("m", "F").WaitOn("m", "F", Waiting)
                 .Move("res:SG", "thread:w0", "wake").Move("res:SG", "thread:w3", "wake");
            })
        .Step("Phase 1: fast workers arrive first",
            "Workers 0 and 1 finish their phase-1 work and wait at the barrier. Workers 2 and 3 are still working.",
            "SignalAndWait atomically increments the arrival count, then sleeps the thread. Two cores are now idle.",
            [7],
            s => s.WaitOn("w0", "B", Waiting).WaitOn("w1", "B", Waiting)
                  .Unschedule("w0").Unschedule("w1")
                  .Set("B", "phase 0, 2/4 arrived"))
        .Step("The last one arrives and everyone moves on",
            "When worker 3 arrives, the post-phase action runs and all four are released together into phase 2.",
            "The last arriving thread runs the callback and then wakes everyone. Participants are counted, not identified: if a worker throws and never arrives, the others wait forever (or until their timeout).",
            [2, 7],
            s =>
            {
                foreach (var w in new[] { "w0", "w1", "w2", "w3" })
                {
                    s.StopWaiting(w, "B");
                }

                s.Run("w0", 0).Run("w1", 1).Run("w2", 2).Run("w3", 3)
                 .Set("B", "phase 1 done, callback ran", Good)
                 .Note("w3", "last arrival runs the callback");
            })
        .Step("Phase 2",
            "The same rendezvous happens again. A Barrier is reusable across phases.",
            "Internally the phase number goes up, so a fast worker cannot be mixed up with the previous phase.",
            [8],
            s =>
            {
                foreach (var w in new[] { "w0", "w1", "w2", "w3" })
                {
                    s.WaitOn(w, "B", Waiting).Unschedule(w);
                }

                s.Set("B", "phase 1, 4/4 arrived", Good);
            })
        .Step("Count down to zero",
            "Each worker signals the CountdownEvent. When it reaches zero, main wakes up.",
            "Signal is an atomic decrement. The thread that brings it to 0 sets the internal event, which wakes main.",
            [9, 13],
            s =>
            {
                foreach (var w in new[] { "w0", "w1", "w2", "w3" })
                {
                    s.StopWaiting(w, "B").Phase(w, Done).Go(w, null);
                }

                s.Set("F", "count = 0 (set)", Good)
                 .StopWaiting("m", "F").Run("m", 0)
                 .Say("Start gate, barrier, latch: three ways to coordinate without locks", Good);
            })
        .Build();

    // ------------------------------------------------------------------------------------------
    private static Story MemoryVisibility() => new StoryBuilder("S4",
            "Correctness needs a memory barrier, not just mutual exclusion. Use volatile, Volatile.Read, Interlocked or lock. This bug often only appears in a Release build.")
        .Setup(s =>
        {
            s.AddCores(2);
            s.AddThread("sp", "spinner");
            s.AddThread("m", "main");
            s.AddResource("F", "_plainFlag", RK.Variable, 0.5, 0.4, value: "false");
            s.Ram("_plainFlag", "false");
        })
        .Code("""
            // spinner thread
            while (!_plainFlag)
            {
                // empty: no call, no barrier, nothing forces a re-read
            }

            // main thread
            Thread.Sleep(200);
            _plainFlag = true;

            // What the Release-mode JIT may legally produce:
            //   mov  al, [_plainFlag]    ; read ONCE
            // loop:
            //   test al, al
            //   je   loop                ; never touches memory again

            // Fix:  private volatile bool _flag;   or   Volatile.Read(ref _flag)
            """)
        .Step("The spinner starts",
            "The spinner loops until _plainFlag becomes true.",
            "Core 0 takes a cache miss, and the line holding _plainFlag arrives in Shared state.",
            [2],
            s => s.Run("sp", 0).Go("sp", "F", Spinning)
                  .Cache(0, "_plainFlag", "false", 'S')
                  .Move("ram:_plainFlag", "core:0", "load false"))
        .Step("The JIT moves the read out of the loop",
            "Because _plainFlag is not volatile and the loop body has no calls or barriers, the optimiser may assume no other thread changes it. It reads the field once into a register.",
            "From now on, the loop only tests register AL and never issues a memory load. 'R' marks a copy held in a register, outside cache coherence.",
            [11, 12, 13, 14, 15],
            s => s.Reg(0, "al = 0 (copied once)")
                  .Cache(0, "_plainFlag", "false (in AL)", 'R')
                  .Note("sp", "checking a register, not memory"))
        .Step("Main writes true",
            "200 ms later, main sets _plainFlag = true.",
            "The store goes to Core 1's store buffer, then to its L1 in M state. The coherence protocol does invalidate Core 0's cached copy within about 100 ns. The hardware has done its job.",
            [8, 9],
            s => s.Run("m", 1).Go("m", "F").Set("F", "true", Good)
                  .Cache(1, "_plainFlag", "true", 'M')
                  .Invalidate(0, "_plainFlag")
                  .Move("core:1", "core:0", "invalidate"))
        .Step("The spinner never sees it",
            "The spinner keeps looping forever at 100% CPU. This is a livelock caused only by the memory model, with no lock involved.",
            "The stale copy is not in the cache but in a register, and coherence does not reach registers. Debug builds do not move the read out of the loop, which is why it 'works on my machine'.",
            [2, 15],
            s => s.Phase("m", Done).Go("m", null)
                  .Reg(0, "al = 0 (stale forever)")
                  .CoreNote(0, "100% busy, never reloads _plainFlag")
                  .Deadlock("sp")
                  .Say("LIVELOCK: 100% CPU on core 0, and the write is never seen", Bad))
        .Step("Fix: volatile",
            "Make the field volatile, or read it with Volatile.Read. The read now happens on every iteration.",
            "volatile forbids keeping the value in a register across iterations. On x86 each read is still a plain mov, because x86 already keeps loads in order. On ARM64 it becomes ldar (load-acquire). The next read misses in the cache and fetches the new value.",
            [17],
            s =>
            {
                s.R("F").Label = "_volatileFlag";
                s.ClearDeadlock()
                 .Reg(0, "mov al,[flag] every iteration")
                 .Cache(0, "_plainFlag", "true", 'S').Cache(1, "_plainFlag", "true", 'S')
                 .Move("core:1", "core:0", "line (true)")
                 .Note("sp", "re-reads memory every iteration");
            })
        .Step("The spinner exits",
            "The loop sees true and ends.",
            "Barriers also control ordering. x86 lets a later load run before an earlier store that is still in the store buffer, and ARM allows almost any reordering. volatile, Interlocked and lock insert exactly the fences that are needed.",
            [2],
            s => s.Phase("sp", Done).Go("sp", null).Reg(0, null)
                  .Say("Visible: volatile, Volatile.Read, Interlocked and lock all add the needed barrier", Good))
        .Build();

    // ------------------------------------------------------------------------------------------
    private static Story AsyncMutex() => new StoryBuilder("S5",
            "SemaphoreSlim.WaitAsync gives async mutual exclusion with timeouts and cancellation. It is not tied to a thread, and it is not reentrant.")
        .Setup(s =>
        {
            s.AddCores(2);
            s.AddThread("a1", "task 1");
            s.AddThread("a2", "task 2");
            s.AddThread("a3", "task 3");
            s.AddResource("G", "gate", RK.Semaphore, 0.15, 0.4, caption: "SemaphoreSlim(1,1)", value: "count = 1");
            s.AddResource("D", "Task.Delay(80)", RK.Task, 0.9, 0.4, value: "-");
        })
        .Code("""
            var gate = new SemaphoreSlim(1, 1);

            async Task WorkerAsync(int id)
            {
                if (!await gate.WaitAsync(TimeSpan.FromSeconds(2), ct)) return;
                try
                {
                    // entered on thread X
                    await Task.Delay(80).ConfigureAwait(false);   // no thread is held here
                    // leaving on thread Y (may be different!)
                }
                finally { gate.Release(); }
            }
            """)
        .Step("Why not lock + await?",
            "Monitor is tied to a thread: Exit must run on the same thread that called Enter. After an await you may resume on a different thread, so C# does not allow await inside a lock block.",
            "Five async workers in the real demo (three shown). Each one is a state machine on the heap. They only use a thread while they are actually running code.",
            [1],
            s => s.Note("a1", "await inside lock? compile error CS1996"))
        .Step("Task 1 enters",
            "WaitAsync succeeds synchronously. The count drops to 0.",
            "This is an Interlocked compare-exchange on the count, and it completes on thread #7 (Core 0).",
            [5, 8],
            s => s.Run("a1", 0).Acquire("a1", "G").Set("G", "count = 0")
                  .Note("a1", "entered on thread #7"))
        .Step("The others wait without blocking a thread",
            "Tasks 2 and 3 call WaitAsync and get incomplete Tasks back. They are waiting, but no thread is blocked.",
            "Each waiter is just a node in a linked list on the heap. There is no kernel wait and no sleeping thread, so the pool threads are free for other work.",
            [5],
            s => s.WaitOn("a2", "G", Waiting).WaitOn("a3", "G", Waiting)
                  .Set("G", "count = 0, 2 async waiters")
                  .CoreNote(1, "free: running other work"))
        .Step("Task 1 awaits while holding the gate",
            "Task 1 awaits Task.Delay inside the protected region. It logically holds the gate, but it uses no thread at all.",
            "The pool thread returns to the pool. Ownership is just 'count == 0', and there are zero threads behind it. This is impossible with Monitor.",
            [9],
            s => s.Go("a1", "D", Waiting).Unschedule("a1").Set("D", "pending 80 ms", Info)
                  .Note("a1", "holds the gate, but no thread")
                  .CoreNote(0, "thread #7 returned to the pool"))
        .Step("Resumes on a different thread and hands over",
            "The delay completes and Task 1 resumes on thread #12. It calls Release, which completes Task 2's WaitAsync.",
            "Release takes the next waiter from the list and completes its Task. That continuation is scheduled on the pool. Releasing from a different thread is legal, and it is the same reason the lock cannot be reentrant: it does not know which thread holds it.",
            [10, 12],
            s => s.Run("a1", 1).Release("a1", "G").Set("D", "done", Good)
                  .Note("a1", "leaving on thread #12")
                  .StopWaiting("a2", "G").Acquire("a2", "G").Run("a2", 0)
                  .Set("G", "count = 0 (passed to task 2)")
                  .Move("res:G", "thread:a2", "complete WaitAsync"))
        .Step("All workers finish",
            "Each worker gets in one at a time. The highest number inside at once is 1.",
            "Always pass a timeout or CancellationToken to WaitAsync. An async 'deadlock' is just a Task that never completes, and it uses no CPU, so it is hard to notice.",
            [],
            s => s.Release("a2", "G").Phase("a1", Done).Phase("a2", Done)
                  .StopWaiting("a3", "G").Phase("a3", Done).Go("a1", null).Go("a2", null).Go("a3", null)
                  .Set("G", "count = 1", Good)
                  .Say("Peak concurrency = 1, and no thread was ever blocked", Good))
        .Build();
}
