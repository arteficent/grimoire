using grimoire.Model;
using static grimoire.Model.Tone;

namespace grimoire.Stories;

internal static class OperatingSystemLessons
{
    public static IEnumerable<Lesson> All()
    {
        yield return ContextSwitch();
        yield return SystemCall();
        yield return CpuScheduling();
        yield return PriorityInversion();
        yield return DiningPhilosophers();
        yield return Bankers();
    }

    // ------------------------------------------------------------------------------------------
    private static Lesson ContextSwitch() =>
        new StoryBuilder("OS1", "A context switch saves one thread's registers and loads another's. Switching between processes also changes the address space, which flushes TLB entries and cools the caches.")
            .Setup(s =>
            {
                s.Region("pa", "Process A (one address space)", 30, 30, 430, 240);
                s.Box("pa-code", "code", 50, 70, 120, 44);
                s.Box("pa-heap", "heap", 180, 70, 120, 44);
                s.Box("t1", "thread T1", 50, 150, 180, 44, "stack + registers");
                s.Box("t2", "thread T2", 250, 150, 180, 44, "stack + registers");
                s.Tint("t1", 0).Tint("t2", 1);
                s.Region("pb", "Process B (another address space)", 30, 300, 430, 230);
                s.Box("pb-code", "code", 50, 340, 120, 44);
                s.Box("pb-heap", "heap", 180, 340, 120, 44);
                s.Box("t3", "thread T3", 50, 420, 180, 44, "stack + registers");
                s.Tint("t3", 2);
                s.Box("core", "CPU core", 560, 50, 380, 120, "idle");
                s.Table("tcb", "kernel: saved thread contexts", 560, 220, 380, "T1: -", "T2: -", "T3: -");
            })
            .NoCode()
            .Step("Processes and threads",
                "A process owns an address space (code, heap, open files). Threads inside it share all of that, but each thread has its own stack and registers.",
                "Creating a thread costs a 1 MB stack reservation and a kernel object. Creating a process also needs new page tables and handle tables, which is far more expensive.")
            .Step("T1 is running",
                "The core's registers (instruction pointer, stack pointer, general-purpose registers) currently hold T1's state.",
                "Only one thread runs on a core at a time. The OS creates the illusion of concurrency by switching between threads many times per second.",
                s => s.Write("core", "CPU core", "running T1: RIP, RSP, RAX... belong to T1").Paint("core", Good).Paint("t1", Good))
            .Step("Timer interrupt",
                "The hardware timer fires, which ends T1's time slice. The CPU jumps into the kernel's interrupt handler.",
                "Time slices are typically 1-15 ms. A thread that blocks on I/O or a lock gives up the core early, without waiting for the timer.",
                s => s.Write("core", "CPU core", "timer interrupt: enter kernel").Paint("core", Warn).Paint("t1", Neutral))
            .Step("Save T1's context",
                "The kernel stores T1's registers in its thread control block so it can resume later exactly where it stopped.",
                "That is about 20 general-purpose registers plus the vector/FPU state (up to several KB with AVX-512). The direct cost of a switch is about 1-5 us.",
                s =>
                {
                    s.Table("tcb", "kernel: saved thread contexts", 560, 220, 380, "T1: RIP=0x4011a0 RSP=... (saved)", "T2: ready", "T3: ready").HotRow("tcb", 0);
                    s.Move("shape:core", "shape:tcb", "save registers", Warn);
                })
            .Step("Switch to T3 in another process",
                "The scheduler picks T3. Its registers are loaded and the page-table base register (CR3) switches to process B's address space.",
                "Changing address space makes TLB entries for process A useless (PCID tags reduce this), and T3's data is probably not in the caches yet. These indirect costs often exceed the direct switch cost.",
                s =>
                {
                    s.HotRow("tcb", 2);
                    s.Move("shape:tcb", "shape:core", "load T3", Info);
                    s.Write("core", "CPU core", "running T3, CR3 -> process B page tables").Paint("core", Info).Paint("t3", Good);
                })
            .Step("Same-process switches are cheaper",
                "Switching from T1 to T2 keeps the same address space: no CR3 change and no TLB flush.",
                "This is one reason thread pools, async/await and green threads exist: they reduce how often the OS has to switch. Thousands of runnable threads mean thousands of switches per second.",
                s =>
                {
                    s.Paint("t3", Neutral).Paint("t2", Good);
                    s.Write("core", "CPU core", "running T2 (same address space as T1)").Paint("core", Good);
                    s.HotRow("tcb", 1);
                    s.Say("Fewer, longer-running threads mean fewer context switches", Good);
                })
            .Lesson("Processes, threads and the context switch", "Processes", "What really happens when the OS swaps one thread for another, and why it costs more than you think.");

    // ------------------------------------------------------------------------------------------
    private static Lesson SystemCall() =>
        new StoryBuilder("OS2", "Every system call crosses from user mode into the kernel and back. Each crossing is cheap but not free, so high-performance I/O batches calls or uses asynchronous completion queues.")
            .Setup(s =>
            {
                s.Region("user", "user mode (ring 3)", 30, 30, 940, 200, Info);
                s.Region("kernel", "kernel mode (ring 0)", 30, 260, 940, 200, Warn);
                s.Box("app", "File.ReadAllBytes()", 60, 90, 200, 56, "your code");
                s.Box("rt", "runtime / libc", 330, 90, 200, 56, "read(fd, buf, n)");
                s.Box("entry", "syscall entry", 330, 320, 200, 56, "save regs, checks");
                s.Box("vfs", "VFS + page cache", 600, 320, 180, 56);
                s.Box("drv", "block driver", 800, 320, 150, 56);
                s.Box("disk", "NVMe SSD", 800, 490, 150, 50, "DMA");
                s.Connect("app", "rt").Connect("rt", "entry", "syscall").Connect("entry", "vfs").Connect("vfs", "drv").Connect("drv", "disk");
            })
            .NoCode()
            .Step("Your code asks for a file",
                "The managed call goes through the runtime to the OS read() function.",
                "Up to this point everything runs in user mode, with no special privileges. User code cannot touch devices or other processes' memory directly.",
                s => s.Paint("app", Info).Paint("rt", Info))
            .Step("Crossing into the kernel",
                "The 'syscall' instruction switches the CPU to kernel mode and jumps to a fixed entry point. The kernel checks every argument.",
                "The mode switch itself costs about 100 ns. Spectre and Meltdown mitigations (page-table isolation, buffer flushes) can multiply that. The kernel must assume every pointer from user space is hostile.",
                s =>
                {
                    s.PaintAll(Neutral, "app", "rt");
                    s.Paint("entry", Warn);
                    s.Move("shape:rt", "shape:entry", "mode switch", Warn);
                })
            .Step("Page cache hit: fast path",
                "If the file is already in the page cache (RAM), the kernel copies it into your buffer and returns immediately.",
                "Repeated reads of hot files never touch the disk. That is why the second run of a program is often much faster than the first.",
                s =>
                {
                    s.Paint("entry", Neutral).Paint("vfs", Good);
                    s.Move("shape:vfs", "shape:app", "copy to user buffer", Good);
                })
            .Step("Cache miss: the thread sleeps",
                "On a miss, the driver programs the SSD to DMA the data into RAM, and your thread blocks. The core runs other threads meanwhile.",
                "DMA lets the device write memory directly, without the CPU copying each byte. A typical NVMe read takes about 50-100 us, which is enough time for tens of thousands of instructions on other threads.",
                s =>
                {
                    s.Paint("vfs", Warn).Paint("drv", Info).Paint("disk", Info).Paint("app", Bad);
                    s.SubText("app", "blocked in kernel");
                    s.Move("shape:drv", "shape:disk", "DMA request", Info);
                })
            .Step("Interrupt, wake up, return",
                "The SSD raises an interrupt when it is done. The kernel marks the thread runnable, and it returns to user mode with the data.",
                "Each read costs two mode switches plus scheduling. Buffered streams, reading in large chunks, and async APIs (IOCP on Windows, io_uring on Linux) reduce the number of crossings.",
                s =>
                {
                    s.PaintAll(Neutral, "drv", "disk", "vfs");
                    s.Paint("app", Good).SubText("app", "your code");
                    s.Move("shape:disk", "shape:app", "interrupt + wake", Good);
                    s.Say("Batch your I/O: fewer kernel crossings = more throughput", Good);
                })
            .Lesson("System calls: crossing into the kernel", "Processes", "The path of a file read from your code through the kernel to the SSD and back.");

    // ------------------------------------------------------------------------------------------
    private static Lesson CpuScheduling()
    {
        (string Name, int Arrival, int Burst)[] procs = [("P1", 0, 8), ("P2", 1, 4), ("P3", 2, 9), ("P4", 3, 5)];
        const double x0 = 60, unit = 33, y = 300;

        List<(string Label, int Start, int Length, int Color)> Fcfs()
        {
            var t = 0;
            var result = new List<(string, int, int, int)>();
            for (var i = 0; i < procs.Length; i++)
            {
                t = Math.Max(t, procs[i].Arrival);
                result.Add((procs[i].Name, t, procs[i].Burst, i));
                t += procs[i].Burst;
            }

            return result;
        }

        List<(string Label, int Start, int Length, int Color)> Sjf()
        {
            var t = 0;
            var left = Enumerable.Range(0, procs.Length).ToList();
            var result = new List<(string, int, int, int)>();
            while (left.Count > 0)
            {
                var ready = left.Where(i => procs[i].Arrival <= t).ToList();
                if (ready.Count == 0)
                {
                    t = left.Min(i => procs[i].Arrival);
                    continue;
                }

                var pick = ready.OrderBy(i => procs[i].Burst).First();
                result.Add((procs[pick].Name, t, procs[pick].Burst, pick));
                t += procs[pick].Burst;
                left.Remove(pick);
            }

            return result;
        }

        void Append(List<(string Label, int Start, int Length, int Color)> result, int proc, int t)
        {
            if (result.Count > 0 && result[^1].Color == proc && result[^1].Start + result[^1].Length == t)
            {
                result[^1] = result[^1] with { Length = result[^1].Length + 1 };
            }
            else
            {
                result.Add((procs[proc].Name, t, 1, proc));
            }
        }

        List<(string Label, int Start, int Length, int Color)> Srtf()
        {
            var remaining = procs.Select(p => p.Burst).ToArray();
            var result = new List<(string Label, int Start, int Length, int Color)>();
            for (var t = 0; remaining.Any(r => r > 0); t++)
            {
                var ready = Enumerable.Range(0, procs.Length).Where(i => procs[i].Arrival <= t && remaining[i] > 0).ToList();
                if (ready.Count == 0)
                {
                    continue;
                }

                var pick = ready.OrderBy(i => remaining[i]).ThenBy(i => i).First();
                remaining[pick]--;
                Append(result, pick, t);
            }

            return result;
        }

        List<(string Label, int Start, int Length, int Color)> RoundRobin(int quantum)
        {
            var remaining = procs.Select(p => p.Burst).ToArray();
            var result = new List<(string Label, int Start, int Length, int Color)>();
            var queue = new Queue<int>();
            var arrived = new HashSet<int>();
            void Arrivals(int t)
            {
                for (var i = 0; i < procs.Length; i++)
                {
                    if (procs[i].Arrival <= t && arrived.Add(i))
                    {
                        queue.Enqueue(i);
                    }
                }
            }

            var time = 0;
            Arrivals(time);
            while (remaining.Any(r => r > 0))
            {
                if (queue.Count == 0)
                {
                    Arrivals(++time);
                    continue;
                }

                var current = queue.Dequeue();
                for (var used = 0; used < quantum && remaining[current] > 0; used++)
                {
                    remaining[current]--;
                    Append(result, current, time);
                    Arrivals(++time);
                }

                if (remaining[current] > 0)
                {
                    queue.Enqueue(current);
                }
            }

            return result;
        }

        string Stats(List<(string Label, int Start, int Length, int Color)> slices)
        {
            var waits = procs.Select((p, i) =>
            {
                var finish = slices.Where(s => s.Color == i).Max(s => s.Start + s.Length);
                return finish - p.Arrival - p.Burst;
            }).ToArray();
            return $"waiting: {string.Join("  ", waits.Select((w, i) => $"{procs[i].Name}={w}"))}   average = {waits.Average():0.##}";
        }

        var fcfs = Fcfs();
        var sjf = Sjf();
        var srtf = Srtf();
        var rr = RoundRobin(3);

        return new StoryBuilder("OS3", "No scheduler is best at everything. FCFS is simple but suffers convoys, shortest-job-first minimises waiting but needs to know job lengths, and round robin trades throughput for responsiveness.")
            .Setup(s =>
            {
                s.Table("procs", "processes", 60, 30, 300, procs.Select(p => $"{p.Name}  arrives {p.Arrival}  needs {p.Burst}").ToArray());
                s.Label("algo", "", 420, 40, 540, 20, Info);
                s.Label("stats", "", 60, y + 90, 900, 14);
                s.Caption("axis", "time ->", 60, y - 24);
            })
            .NoCode()
            .Step("Four processes compete for one core",
                "Each process needs some CPU time (its burst). The scheduler decides who runs and when.",
                "Waiting time = finish - arrival - burst. Lower average waiting means a snappier system, but fairness and starvation also matter.")
            .Step("First come, first served",
                "Run jobs strictly in arrival order. Simple, but short jobs get stuck behind long ones.",
                "This is the convoy effect: P2 needs only 4 units but waits 7 behind P1. FIFO queues in front of a slow resource (a disk, a database) behave the same way.",
                s =>
                {
                    s.Write("algo", "FCFS (non-preemptive)");
                    s.Gantt("g", fcfs, x0, y, unit);
                    s.Write("stats", Stats(fcfs));
                })
            .Step("Shortest job first",
                "Whenever the core is free, pick the shortest ready job. P2 and P4 jump ahead of P3.",
                "SJF is provably optimal for average waiting (among non-preemptive schedules), but it needs to know job lengths in advance, and long jobs can starve.",
                s =>
                {
                    s.Write("algo", "SJF (non-preemptive)");
                    s.Gantt("g", sjf, x0, y, unit);
                    s.Write("stats", Stats(sjf));
                })
            .Step("Shortest remaining time (preemptive)",
                "When a shorter job arrives, it preempts the running one. P1 is interrupted at t=1 by P2.",
                "Preemption lowers the average wait even further, but every preemption costs a context switch.",
                s =>
                {
                    s.Write("algo", "SRTF (preemptive SJF)");
                    s.Gantt("g", srtf, x0, y, unit);
                    s.Write("stats", Stats(srtf));
                })
            .Step("Round robin, quantum = 3",
                "Each job runs for at most 3 units, then goes to the back of the queue. Everyone gets the CPU quickly.",
                "Response time (time until first run) is excellent, but the average completion time is worse and there are many more switches. Real OS schedulers (Linux CFS/EEVDF, Windows) blend priorities, fairness and responsiveness.",
                s =>
                {
                    s.Write("algo", "Round robin (q = 3)");
                    s.Gantt("g", rr, x0, y, unit);
                    s.Write("stats", Stats(rr));
                    s.Say("Lower waiting time (SJF) vs. faster first response (RR)", Info);
                })
            .Lesson("CPU scheduling: FCFS, SJF, SRTF, round robin", "Scheduling", "The same four jobs under four schedulers, and the waiting time each produces.");
    }

    // ------------------------------------------------------------------------------------------
    private static Lesson PriorityInversion()
    {
        const double x0 = 170, unit = 52;
        (string Name, double Y, int Color)[] rows = [("High", 120, 3), ("Medium", 220, 4), ("Low", 320, 2)];

        void Seg(SceneState s, string id, int row, int start, int end, string text, Tone tone = Neutral)
        {
            s.Box(id, text, x0 + start * unit, rows[row].Y, Math.Max(10, (end - start) * unit - 3), 50, tone: tone);
            s.Shape(id).FontSize = 11;
            if (tone == Neutral)
            {
                s.Tint(id, rows[row].Color);
            }
        }

        return new StoryBuilder("OS4", "Priority inversion happens when a high-priority task waits on a lock held by a low-priority task that keeps getting preempted. Priority inheritance temporarily boosts the lock holder.")
            .Setup(s =>
            {
                for (var r = 0; r < rows.Length; r++)
                {
                    s.Label("row" + r, rows[r].Name + " priority", 30, rows[r].Y + 14, 140, 13);
                }

                for (var t = 0; t <= 14; t += 2)
                {
                    s.Label("tick" + t, t.ToString(), x0 + t * unit - 4, 390, 30, 10.5);
                }
            })
            .NoCode()
            .Step("Three tasks, one shared lock",
                "Low and High both use a shared bus mutex. Medium never touches it.",
                "This is the setup from NASA's Mars Pathfinder (1997). A low-priority meteorology task and a high-priority bus-management task shared a mutex.")
            .Step("Low takes the lock",
                "Low runs and acquires the mutex at t = 1.",
                "Nothing is wrong yet. Low expects to hold the lock only briefly.",
                s =>
                {
                    Seg(s, "l1", 2, 0, 1, "run");
                    Seg(s, "l2", 2, 1, 2, "lock", Warn);
                })
            .Step("High preempts, then blocks on the lock",
                "High becomes ready, preempts Low, runs until it needs the mutex at t = 3, and blocks.",
                "This part is expected: High must wait until Low finishes its short critical section.",
                s =>
                {
                    Seg(s, "h1", 0, 2, 3, "run");
                    Seg(s, "h2", 0, 3, 4, "wait", Bad);
                    Seg(s, "l3", 2, 3, 4, "lock", Warn);
                })
            .Step("Medium preempts Low: inversion",
                "Medium becomes ready at t = 4. It outranks Low, so it runs. Low cannot finish and release the lock, and High waits behind Medium.",
                "High's wait is now bounded by Medium's run time, not by Low's tiny critical section. With several medium tasks it is unbounded. On Pathfinder a watchdog saw the bus task miss its deadline and kept resetting the spacecraft.",
                s =>
                {
                    s.Resize("h2", 8 * unit - 3, 50).Write("h2", "waiting for Low... behind Medium!");
                    Seg(s, "m1", 1, 4, 10, "Medium runs (no lock needed)");
                    Seg(s, "l4", 2, 10, 11, "unlock", Good);
                    Seg(s, "h3", 0, 11, 13, "run");
                    s.Say("High-priority task waits for a medium-priority one: priority inversion", Bad);
                })
            .Step("Fix: priority inheritance",
                "When High blocks on the mutex, Low temporarily inherits High's priority. Medium can no longer preempt it, so Low releases the lock at t = 4.",
                "NASA fixed Pathfinder remotely by enabling priority inheritance on that VxWorks mutex. Linux has PI futexes, and Windows boosts lock holders on similar principles. Priority ceiling protocols are an alternative.",
                s =>
                {
                    s.Remove("h2", "m1", "l4", "h3");
                    Seg(s, "h2", 0, 3, 4, "wait", Warn);
                    s.Write("l3", "boost").Resize("l3", unit - 3, 50);
                    Seg(s, "l4", 2, 4, 5, "unlock", Good);
                    Seg(s, "h3", 0, 5, 7, "run", Good);
                    Seg(s, "m1", 1, 7, 13, "Medium runs");
                    s.Say("High finishes at t = 7 instead of t = 13", Good);
                })
            .Lesson("Priority inversion (the Mars Pathfinder bug)", "Scheduling", "How a medium-priority task can starve a high-priority one, and how priority inheritance fixes it.");
    }

    // ------------------------------------------------------------------------------------------
    private static Lesson DiningPhilosophers()
    {
        const double cx = 500, cy = 270;

        (double X, double Y) At(double angleDeg, double r) =>
            (cx + r * Math.Cos(angleDeg * Math.PI / 180), cy + r * Math.Sin(angleDeg * Math.PI / 180));

        return new StoryBuilder("OS5", "Five philosophers each grabbing their left fork deadlock instantly. Ordering resources globally (always take the lower-numbered fork first) or limiting how many may sit removes the cycle.")
            .Setup(s =>
            {
                s.Circle("table", "table", cx, cy, 70, tone: Neutral);
                for (var i = 0; i < 5; i++)
                {
                    var (px, py) = At(-90 + i * 72, 200);
                    s.Circle("p" + i, "P" + i, px, py, 32, "thinking");
                    s.Tint("p" + i, i);
                    var (fx, fy) = At(-90 + i * 72 + 36, 120);
                    s.Box("f" + i, "fork " + i, fx - 34, fy - 16, 68, 32);
                }
            })
            .NoCode()
            .Step("Five philosophers, five forks",
                "Each philosopher needs both neighbouring forks to eat. P(i)'s left fork is fork i, and its right fork is fork i-1.",
                "Forks are locks, philosophers are threads. The same structure appears with database rows, account transfers, or any system with pairs of shared resources.")
            .Step("Everyone picks up their left fork",
                "All five grab their left fork at the same moment, and every fork is now held.",
                "Each acquisition succeeds. Nothing looks wrong yet. This is hold-and-wait in progress.",
                s =>
                {
                    for (var i = 0; i < 5; i++)
                    {
                        s.Connect("p" + i, "f" + i, "holds", Good).Paint("f" + i, Info).SubText("p" + i, "has left fork");
                    }
                })
            .Step("Everyone waits for their right fork",
                "Each philosopher waits for the fork held by their neighbour. The waits form a cycle of five, so it is a deadlock.",
                "All four Coffman conditions hold: mutual exclusion (one owner per fork), hold-and-wait, no pre-emption, and circular wait.",
                s =>
                {
                    for (var i = 0; i < 5; i++)
                    {
                        s.Connect("p" + i, "f" + ((i + 4) % 5), "waits", Bad, dashed: true, animate: true);
                        s.Paint("p" + i, Bad).SubText("p" + i, "starving");
                    }

                    s.Say("DEADLOCK: circular wait around the table", Bad);
                })
            .Step("Fix 1: take the lower-numbered fork first",
                "With a global order, P0 reaches for fork 0 first, the same fork as P1. One of them waits holding nothing, so the cycle can never close.",
                "This is the lock-ordering fix from the concurrency track (D2) applied to five locks. P4 picks up fork 3 then fork 4 and eats, then releases both, and the others proceed in turn.",
                s =>
                {
                    s.ClearLinks();
                    for (var i = 0; i < 5; i++)
                    {
                        s.Paint("p" + i, Neutral).Tint("p" + i, i).SubText("p" + i, "thinking").Paint("f" + i, Neutral);
                    }

                    s.Connect("p0", "f0", "holds", Good).Connect("p1", "f0", "waits", Warn, dashed: true, animate: true);
                    s.Connect("p2", "f1", "holds", Good).Connect("p3", "f2", "holds", Good);
                    s.Connect("p4", "f3", "holds", Good).Connect("p4", "f4", "holds", Good);
                    s.Paint("p4", Good).SubText("p4", "eating").SubText("p1", "waits, holding nothing");
                })
            .Step("Fix 2: a waiter allows only four at the table",
                "A semaphore with 4 permits guards the table. With at most 4 philosophers competing for 5 forks, at least one can always get both.",
                "This breaks hold-and-wait across the whole group. Both fixes avoid deadlock, but fairness (no philosopher starves forever) still needs a fair queue or ticketing.",
                s =>
                {
                    s.ClearLinks();
                    s.Paint("p4", Neutral).Tint("p4", 4).SubText("p4", "outside: waits for a permit").Ghost("p4");
                    s.Paint("p1", Neutral).SubText("p1", "thinking");
                    s.Say("Semaphore(4): at most 4 diners, so a cycle of 5 is impossible", Good);
                })
            .Lesson("Dining philosophers", "Deadlock", "The classic deadlock puzzle, and two standard ways to make it impossible.");
    }

    // ------------------------------------------------------------------------------------------
    private static Lesson Bankers()
    {
        int[][] alloc = [[0, 1, 0], [2, 0, 0], [3, 0, 2], [2, 1, 1], [0, 0, 2]];
        int[][] max = [[7, 5, 3], [3, 2, 2], [9, 0, 2], [2, 2, 2], [4, 3, 3]];
        var need = alloc.Select((a, i) => a.Select((v, j) => max[i][j] - v).ToArray()).ToArray();
        var work = new[] { 3, 3, 2 };
        var finished = new bool[5];

        static string V(int[] v) => string.Join(" ", v);

        string[] Rows() => Enumerable.Range(0, 5)
            .Select(i => $"P{i}  alloc {V(alloc[i])}  max {V(max[i])}  need {V(need[i])}{(finished[i] ? "  done" : string.Empty)}")
            .ToArray();

        var b = new StoryBuilder("OS6", "The Banker's algorithm only grants a request if, afterwards, some order still exists in which every process can finish. It avoids deadlock, but it needs every process's maximum demand up front.")
            .Setup(s =>
            {
                s.Table("t", "resources A B C", 60, 40, 560, Rows());
                s.Box("work", "available: 3 3 2", 680, 60, 260, 60, "work vector", Info);
                s.Label("seq", "safe sequence: (none yet)", 60, 230, 880, 16);
            })
            .NoCode();

        b.Step("Is this state safe?",
            "Five processes hold some resources and may still ask for up to 'need' more. We try to find an order in which all of them can finish.",
            "Safe means there is at least one completion order. Unsafe does not mean deadlocked yet, but the OS can no longer guarantee that deadlock is avoided.");

        var sequence = new List<string>();
        var progress = true;
        while (progress)
        {
            progress = false;
            for (var i = 0; i < 5; i++)
            {
                if (finished[i])
                {
                    continue;
                }

                var fits = need[i].Zip(work).All(p => p.First <= p.Second);
                var (idx, before) = (i, V(work));
                if (fits)
                {
                    for (var j = 0; j < 3; j++)
                    {
                        work[j] += alloc[i][j];
                    }

                    finished[i] = true;
                    sequence.Add("P" + i);
                }

                var (rows, after, seq) = (Rows(), V(work), string.Join(" -> ", sequence));
                b.Step(fits ? $"P{i} can finish" : $"P{i} must wait",
                    fits
                        ? $"P{i} needs {V(need[i])}, which fits within {before}. Let it run to completion; it returns its allocation, so available grows to {after}."
                        : $"P{i} needs {V(need[i])}, which exceeds the available {before}. Skip it for now.",
                    idx == 0 && !fits ? "The scan is greedy: any process that can finish is safe to run first, because finishing only ever returns resources." : string.Empty,
                    s =>
                    {
                        s.Table("t", "resources A B C", 60, 40, 560, rows).HotRow("t", idx);
                        s.Paint("t", fits ? Good : Warn);
                        s.Write("work", "available: " + after).Paint("work", fits ? Good : Info);
                        s.Write("seq", "safe sequence: " + (seq.Length == 0 ? "(none yet)" : seq));
                    });
                if (fits)
                {
                    progress = true;
                    break;
                }
            }
        }

        b.Step("The state is safe",
            $"Order {string.Join(" -> ", sequence)} lets every process finish, so the current allocation is safe.",
            "For each request, the OS pretends to grant it, runs this check, and rolls back if the result is unsafe. The check is O(n^2 * m), and real systems rarely know maximum demands. That is why general-purpose OSes prefer lock ordering, timeouts and detection instead.",
            s =>
            {
                s.Paint("t", Good).HotRow("t");
                s.Say("Safe: a completion order exists for all 5 processes", Good);
            });

        return b.Lesson("Banker's algorithm (deadlock avoidance)", "Deadlock", "Granting resources only when a safe completion order still exists.");
    }
}
