using grimoire.Model;
using static grimoire.Model.Tone;

namespace grimoire.Stories;

internal static class MemoryLessons
{
    public static IEnumerable<Lesson> All()
    {
        yield return StackVsHeap();
        yield return VirtualMemory();
        yield return PageReplacement();
        yield return Thrashing();
        yield return Fragmentation();
        yield return BuddyAllocator();
        yield return GarbageCollection();
        yield return ManagedLeaks();
        yield return CacheLocality();
        yield return FalseSharing();
    }

    // ------------------------------------------------------------------------------------------
    private static Lesson StackVsHeap() =>
        new StoryBuilder("MM1", "The stack holds short-lived frames that vanish on return at zero cost. The heap holds objects that outlive the call, and the GC must later find and reclaim them.")
            .Setup(s =>
            {
                s.Region("stack", "stack (one per thread, grows down)", 30, 30, 320, 500, Info);
                s.Region("heap", "managed heap (shared, garbage collected)", 380, 30, 590, 500, Warn);
            })
            .Code("""
                void Main()
                {
                    var order = new Order(42);     // reference on the stack, object on the heap
                    Process(order);
                }
                void Process(Order o)
                {
                    int total = o.Count * 2;       // value type: lives in the frame
                    var tmp = new byte[1024];      // heap allocation
                }                                  // frame popped; tmp is now garbage
                """)
            .Step("Main allocates an Order",
                "The local variable 'order' is a reference stored in Main's stack frame. The object itself lives on the heap.",
                "Allocating on the .NET heap is usually just a pointer bump inside the thread's allocation context (about 10-20 ns). The expensive part is collecting it later.",
                [3],
                s =>
                {
                    s.Box("fmain", "Main()", 50, 440, 280, 70, "order -> 0x2A10");
                    s.Box("order", "Order { Id = 42 }", 420, 80, 220, 56, "0x2A10", Good);
                    s.Connect("fmain", "order");
                })
            .Step("Process() gets its own frame",
                "Calling Process pushes a new frame with its parameters and locals. 'total' is an int, so it lives right in the frame. 'tmp' points to a new array on the heap.",
                "Pushing a frame is one subtraction from the stack pointer register, which is essentially free. Value types (int, struct) live inline wherever they are declared; reference types always live on the heap.",
                [7, 8, 9],
                s =>
                {
                    s.Box("fproc", "Process(o)", 50, 360, 280, 70, "o -> 0x2A10, total = 84, tmp -> 0x2B00", Info);
                    s.Box("tmp", "byte[1024]", 420, 200, 220, 56, "0x2B00", Info);
                    s.Connect("fproc", "order").Connect("fproc", "tmp");
                })
            .Step("Return: the frame disappears instantly",
                "When Process returns, its frame is simply popped. Nothing references the byte array any more, so it is garbage.",
                "No cleanup code runs for stack memory: the stack pointer moves back up and the space is reused by the next call. The heap array stays in memory until a GC notices it is unreachable.",
                [10],
                s =>
                {
                    s.Remove("fproc");
                    s.Paint("tmp", Bad).Ghost("tmp").SubText("tmp", "unreachable: garbage");
                })
            .Step("The GC reclaims the garbage later",
                "At the next gen0 collection the array is found to be unreachable and its memory is reused. The Order survives because Main still references it.",
                "The GC traces from the roots (stack slots, static fields, registers, GC handles). Anything it cannot reach is free. Short-lived garbage like 'tmp' is the cheapest kind to collect.",
                [],
                s => s.Remove("tmp"))
            .Step("Too much stack: overflow",
                "Each nested call adds a frame. Infinite or very deep recursion exhausts the stack (1 MB by default on Windows, often 8 MB on Linux).",
                ".NET cannot recover from StackOverflowException: the process terminates. Use iteration, an explicit Stack<T>, or raise the thread's stack size. 'stackalloc' with Span<T> is great for small, short-lived buffers, but keep them small.",
                [],
                s =>
                {
                    for (var i = 0; i < 6; i++)
                    {
                        s.Box("rec" + i, $"Recurse() #{i + 1}", 50, 370 - i * 56, 280, 48, tone: i == 5 ? Bad : Warn);
                    }

                    s.Say("Stack exhausted: StackOverflowException kills the process", Bad);
                })
            .Lesson("Stack vs heap", "Allocation", "Where values live, why stack memory is free, and what the GC must clean up.");

    // ------------------------------------------------------------------------------------------
    private static Lesson VirtualMemory() =>
        new StoryBuilder("MM2", "Every memory access is translated from a virtual to a physical address. The TLB makes that free on a hit; a miss costs a page walk, and a missing page costs a page fault.")
            .Setup(s =>
            {
                s.Box("va", "virtual address 0x7F3A1234_5ABC", 260, 30, 480, 50, "page number (VPN) | 12-bit offset", Info);
                s.Table("tlb", "TLB (translation cache)", 40, 130, 280, "VPN 0x7F3A0 -> frame 0x812", "VPN 0x00401 -> frame 0x0A3", "(64 L1 entries, ~1.5k L2)");
                string[] levels = ["PML4", "PDPT", "PD", "PT"];
                for (var i = 0; i < 4; i++)
                {
                    s.Box("pw" + i, levels[i], 380 + i * 150, 160, 120, 50, "9 bits");
                    if (i > 0)
                    {
                        s.Connect("pw" + (i - 1), "pw" + i);
                    }
                }

                s.Region("ram", "physical memory (4 KB frames)", 380, 300, 590, 220);
                for (var i = 0; i < 6; i++)
                {
                    s.Box("fr" + i, $"frame 0x{0x3F0 + i:X3}", 400 + i * 94, 360, 86, 60);
                }
            })
            .NoCode()
            .Step("Virtual addresses",
                "Your program never sees physical RAM addresses. Each process has its own virtual address space, split into 4 KB pages.",
                "The low 12 bits are the offset within the page. The remaining bits identify the virtual page, which the CPU must translate into a physical frame on every single load and store.")
            .Step("Check the TLB: miss",
                "The CPU first looks in the TLB, a small cache of recent translations. This page is not in it.",
                "A TLB hit adds roughly zero latency. On a miss the hardware page walker (on x86) must read the page tables from memory.",
                s =>
                {
                    s.Paint("tlb", Warn).Paint("va", Info);
                    s.Move("shape:va", "shape:tlb", "lookup", Warn);
                })
            .Step("Page walk: four dependent reads",
                "The walker uses 9 bits of the address at each of four levels to find the next table, ending at the page-table entry (PTE).",
                "That is up to 4 extra memory accesses before your actual load can start. Paging-structure caches soften this, but random access over a huge data set can be dominated by TLB misses.",
                s =>
                {
                    s.Paint("tlb", Neutral);
                    s.PaintAll(Info, "pw0", "pw1", "pw2", "pw3");
                    for (var i = 0; i < 3; i++)
                    {
                        s.Move("shape:pw" + i, "shape:pw" + (i + 1), "read", Info);
                    }
                })
            .Step("Translate and fill the TLB",
                "The PTE says this page lives in frame 0x3F2, and it records permission bits (present, writable, user, no-execute). Physical address = frame + offset.",
                "The new translation is cached in the TLB, so the next access to anywhere in this 4 KB page is fast.",
                s =>
                {
                    s.PaintAll(Neutral, "pw0", "pw1", "pw2");
                    s.Paint("pw3", Good).Paint("fr2", Good);
                    s.Move("shape:pw3", "shape:fr2", "frame 0x3F2", Good);
                    s.Table("tlb", "TLB (translation cache)", 40, 130, 280, "VPN 0x7F3A0 -> frame 0x812", "VPN 0x00401 -> frame 0x0A3", "VPN 0x7F3A1 -> frame 0x3F2 (new)").HotRow("tlb", 2);
                })
            .Step("Next access: TLB hit",
                "Another access in the same page hits the TLB and goes straight to memory.",
                "The TLB covers only a few MB with 4 KB pages. Huge pages (2 MB / 1 GB) cover far more memory per entry, which is why databases, JVMs and .NET can be configured to use them for large heaps.",
                s =>
                {
                    s.PaintAll(Neutral, "pw3");
                    s.Paint("tlb", Good);
                    s.Move("shape:tlb", "shape:fr2", "hit", Good);
                })
            .Step("When the page is not present: page fault",
                "If the PTE says 'not present', the CPU raises a page fault. The kernel finds or allocates a frame, fills it (zeros, or data from disk), updates the PTE and retries the instruction.",
                "A minor fault (zero-fill, or a page already in the page cache) costs about a microsecond. A major fault reads from disk and costs 100 us to 10 ms. This is how lazy allocation, memory-mapped files and swapping work.",
                s =>
                {
                    s.Paint("tlb", Neutral).Paint("fr2", Neutral);
                    s.Paint("fr5", Bad).Write("fr5", "not present");
                    s.Say("Page fault: the kernel loads the page, fixes the PTE, retries", Warn);
                })
            .Lesson("Virtual memory, page tables and the TLB", "Virtual memory", "How every address is translated, what a TLB miss costs, and what a page fault really is.");

    // ------------------------------------------------------------------------------------------
    private static Lesson PageReplacement()
    {
        int[] refs = [7, 0, 1, 2, 0, 3, 0, 4, 2, 3];
        const int frames = 3;

        var b = new StoryBuilder("MM3", "With limited RAM, every page fault may evict another page. LRU approximates 'evict what will not be needed soon' better than FIFO, and real kernels approximate LRU cheaply with the CLOCK algorithm.")
            .Setup(s =>
            {
                s.Caption("refcap", "page reference string", 60, 30);
                s.Cells("r", refs.Select(r => r.ToString()).ToArray(), 60, 52, 64, 46, 8, tags: false);
                s.Region("fifo", "FIFO: evict the oldest page", 40, 150, 440, 360);
                s.Region("lru", "LRU: evict the least recently used", 520, 150, 440, 360);
                for (var i = 0; i < frames; i++)
                {
                    s.Box("ff" + i, "-", 100, 200 + i * 80, 320, 60, $"frame {i}");
                    s.Box("lf" + i, "-", 580, 200 + i * 80, 320, 60, $"frame {i}");
                }

                s.Label("fstat", "faults 0", 100, 450, 300, 15);
                s.Label("lstat", "faults 0", 580, 450, 300, 15);
            })
            .NoCode();

        var fifoFrames = new List<int>();
        var fifoQueue = new Queue<int>();
        var lruFrames = new List<int>();
        var lastUse = new Dictionary<int, int>();
        int fifoFaults = 0, lruFaults = 0;

        b.Step("Three frames, ten page references",
            "Both policies see the same sequence of page accesses. A fault means the page has to be loaded and possibly another evicted.",
            "Belady's optimal policy evicts the page used furthest in the future. It is unbeatable but needs the future, so real systems approximate it with the past.");

        for (var t = 0; t < refs.Length; t++)
        {
            var page = refs[t];
            string fifoEvent;
            var fifoHit = fifoFrames.Contains(page);
            var fifoSlot = fifoFrames.IndexOf(page);
            if (fifoHit)
            {
                fifoEvent = "hit";
            }
            else
            {
                fifoFaults++;
                if (fifoFrames.Count < frames)
                {
                    fifoFrames.Add(page);
                    fifoSlot = fifoFrames.Count - 1;
                    fifoEvent = "fault, free frame";
                }
                else
                {
                    var victim = fifoQueue.Dequeue();
                    fifoSlot = fifoFrames.IndexOf(victim);
                    fifoFrames[fifoSlot] = page;
                    fifoEvent = $"fault, evict {victim}";
                }

                fifoQueue.Enqueue(page);
            }

            string lruEvent;
            var lruHit = lruFrames.Contains(page);
            var lruSlot = lruFrames.IndexOf(page);
            if (lruHit)
            {
                lruEvent = "hit";
            }
            else
            {
                lruFaults++;
                if (lruFrames.Count < frames)
                {
                    lruFrames.Add(page);
                    lruSlot = lruFrames.Count - 1;
                    lruEvent = "fault, free frame";
                }
                else
                {
                    var victim = lruFrames.OrderBy(p => lastUse[p]).First();
                    lruSlot = lruFrames.IndexOf(victim);
                    lruFrames[lruSlot] = page;
                    lruEvent = $"fault, evict {victim}";
                }
            }

            lastUse[page] = t;
            var (ff, lf, idx, fs, ls, ffc, lfc) = (fifoFrames.ToArray(), lruFrames.ToArray(), t, fifoSlot, lruSlot, fifoFaults, lruFaults);
            b.Step($"Access page {page}",
                $"FIFO: {fifoEvent}. LRU: {lruEvent}.",
                t == 6 ? "Page 0 is used constantly. LRU keeps it, while FIFO evicted it at the previous step only because it was loaded first." : string.Empty,
                s =>
                {
                    for (var i = 0; i < refs.Length; i++)
                    {
                        s.Paint("r" + i, i == idx ? Info : Neutral).Ghost("r" + i, i < idx);
                    }

                    for (var i = 0; i < frames; i++)
                    {
                        s.Write("ff" + i, i < ff.Length ? $"page {ff[i]}" : "-").Paint("ff" + i, i == fs ? (fifoHit ? Good : Warn) : Neutral);
                        s.Write("lf" + i, i < lf.Length ? $"page {lf[i]}" : "-").Paint("lf" + i, i == ls ? (lruHit ? Good : Warn) : Neutral);
                    }

                    s.Write("fstat", $"faults {ffc}");
                    s.Write("lstat", $"faults {lfc}");
                });
        }

        b.Step("Result",
            $"FIFO: {fifoFaults} faults. LRU: {lruFaults} faults. Keeping recently used pages pays off.",
            "Exact LRU needs a timestamp update on every memory access, which is too expensive. Kernels use CLOCK (second chance): the hardware sets an 'accessed' bit, and the evictor sweeps frames, clearing the bit and evicting the first page whose bit is already clear. FIFO can even get worse with more frames (Belady's anomaly).",
            s => s.Say($"FIFO {fifoFaults} faults vs LRU {lruFaults} faults", Good));

        return b.Lesson("Page replacement: FIFO vs LRU", "Virtual memory", "Which page to evict when RAM is full, run side by side on the same accesses.");
    }

    // ------------------------------------------------------------------------------------------
    private static Lesson Thrashing()
    {
        int[] util = [20, 42, 64, 80, 88, 55, 25, 10];

        void Bars(SceneState s, int upTo, int highlight)
        {
            for (var i = 0; i < util.Length; i++)
            {
                var h = i < upTo ? util[i] * 4 : 0;
                var id = "u" + i;
                if (!s.Has(id))
                {
                    s.Box(id, string.Empty, 110 + i * 105, 470, 80, 1);
                }

                s.Place(id, 110 + i * 105, 470 - h).Resize(id, 80, Math.Max(1, h))
                 .Write(id, i < upTo ? $"{util[i]}%" : string.Empty)
                 .SubText(id, null)
                 .Paint(id, i >= 5 && i < upTo ? Bad : i == highlight ? Good : Info)
                 .Ghost(id, i >= upTo);
                s.Label("x" + i, $"{i + 1} procs", 115 + i * 105, 480, 90, 11);
            }
        }

        return new StoryBuilder("MM4", "When the combined working sets exceed RAM, processes spend their time waiting for page faults instead of running. Adding more work makes it worse; the fix is to reduce concurrency or add memory.")
            .Setup(s => s.Label("ylab", "CPU utilisation", 30, 40, 300, 14))
            .NoCode()
            .Step("More processes, more overlap",
                "With a few processes, while one waits for I/O another can use the CPU, so utilisation climbs.",
                "This is why OSes run many processes at once: overlapping computation with I/O keeps the CPU busy.",
                s => Bars(s, 3, -1))
            .Step("The sweet spot",
                "Around 5 processes the CPU is nearly fully used and every working set still fits in RAM.",
                "A working set is the set of pages a process touches over a short window. As long as the sum of working sets fits in RAM, page faults are rare.",
                s => Bars(s, 5, 4))
            .Step("Past the cliff: thrashing",
                "Add one more process and the working sets no longer fit. Every process keeps faulting on pages that another process just evicted.",
                "Each major fault means a disk read of roughly 0.1-10 ms. The CPU sits idle waiting on the disk, so utilisation collapses while the disk is at 100%.",
                s =>
                {
                    Bars(s, 8, 4);
                    s.Say("Thrashing: busy disk, idle CPU, no progress", Bad);
                })
            .Step("The vicious circle and the fixes",
                "A naive scheduler sees an idle CPU and admits more work, which makes things worse. The fix is to admit less: suspend processes, limit concurrency, or add RAM.",
                "Modern equivalents: containers hitting their memory limit (heavy reclaim, then the OOM killer), JVM or .NET heaps sized above RAM, and services that queue unbounded requests. Cap concurrency and keep swapping off latency-critical paths.",
                s =>
                {
                    Bars(s, 5, 4);
                    s.Say("Admission control: keep total working sets below RAM", Good);
                })
            .Lesson("Thrashing", "Virtual memory", "Why adding work past a memory limit makes a system slower, not faster.");
    }

    // ------------------------------------------------------------------------------------------
    private static Lesson Fragmentation()
    {
        const double x0 = 50, unit = 9;
        var segments = new List<(int Start, int Size, string? Owner)> { (0, 100, null) };
        var b = new StoryBuilder("MM5", "External fragmentation means there is enough free memory in total, but not in one piece. Allocators fight it with size classes, pooling and compaction.")
            .Setup(s =>
            {
                s.Caption("cap", "heap: 100 KB, first-fit allocator", 50, 120);
                s.Label("stats", "free 100 KB, largest hole 100 KB", 50, 330, 900, 16);
            })
            .NoCode();

        void Render(SceneState s, string? highlight, Tone tone)
        {
            s.RemoveWhere(sh => sh.Id.StartsWith("seg", StringComparison.Ordinal));
            foreach (var (start, size, owner) in segments)
            {
                var id = "seg" + start;
                s.Box(id, size < 8 ? $"{size}K" : owner is null ? $"free {size}K" : $"{owner} {size}K", x0 + start * unit, 160, size * unit - 2, 100);
                if (owner is null)
                {
                    s.Ghost(id);
                }
                else
                {
                    s.Tint(id, owner[0] - 'A');
                }

                if (owner is not null && owner == highlight)
                {
                    s.Paint(id, tone).Tint(id, -1);
                }
            }

            var free = segments.Where(x => x.Owner is null).ToList();
            s.Write("stats", $"free {free.Sum(f => f.Size)} KB, largest hole {(free.Count == 0 ? 0 : free.Max(f => f.Size))} KB");
        }

        bool Alloc(string name, int size)
        {
            var i = segments.FindIndex(x => x.Owner is null && x.Size >= size);
            if (i < 0)
            {
                return false;
            }

            var hole = segments[i];
            segments[i] = (hole.Start, size, name);
            if (hole.Size > size)
            {
                segments.Insert(i + 1, (hole.Start + size, hole.Size - size, null));
            }

            return true;
        }

        void Free(string name)
        {
            var i = segments.FindIndex(x => x.Owner == name);
            segments[i] = segments[i] with { Owner = null };
            for (var k = segments.Count - 2; k >= 0; k--)
            {
                if (segments[k].Owner is null && segments[k + 1].Owner is null)
                {
                    segments[k] = segments[k] with { Size = segments[k].Size + segments[k + 1].Size };
                    segments.RemoveAt(k + 1);
                }
            }
        }

        b.Step("An empty heap", "100 KB of contiguous free memory. A first-fit allocator scans for the first hole that is big enough.",
            "malloc implementations keep free lists of holes. First-fit is fast, best-fit leaves smaller leftovers, and both fragment over time.",
            s => Render(s, null, Neutral));

        foreach (var (name, size) in new[] { ("A", 20), ("B", 15), ("C", 25), ("D", 10), ("E", 20) })
        {
            Alloc(name, size);
            var n = name;
            b.Step($"Allocate {name} ({size} KB)", $"{name} takes the first hole that fits.", string.Empty, s => Render(s, n, Info));
        }

        Free("B");
        b.Step("Free B", "B's 15 KB becomes a hole between A and C.",
            "Freeing never moves neighbours. The hole stays exactly where B was.",
            s => Render(s, null, Neutral));
        Free("D");
        b.Step("Free D", "D's 10 KB becomes another hole. Free memory is now spread over three separate places.", string.Empty, s => Render(s, null, Neutral));
        Alloc("F", 12);
        b.Step("Allocate F (12 KB)", "First-fit puts F into B's old 15 KB hole, leaving a useless 3 KB sliver.",
            "Tiny leftovers like this accumulate. Size-class allocators (jemalloc, mimalloc, the .NET GC's free lists) round sizes into buckets to limit slivers.",
            s => Render(s, "F", Info));
        var ok = Alloc("G", 18);
        b.Step("Allocate G (18 KB): fails",
            "There are 23 KB free in total, but no single hole is 18 KB. The allocation fails, or the heap has to grow.",
            "This is external fragmentation. A moving GC fixes it by compacting live objects together. The .NET Large Object Heap (objects of 85,000 bytes or more) is not compacted by default, so long-running services can fragment it. Pool big buffers with ArrayPool<T>.",
            s =>
            {
                Render(s, null, Neutral);
                s.Box("req", "G needs 18K", 400, 400, 200, 50, tone: Bad);
                s.Say(ok ? "Allocated" : "Out of memory with 23 KB free: fragmentation", Bad);
            });
        b.Step("Fix: compaction",
            "Slide the live blocks together and every hole merges into one large free region. G now fits easily.",
            "Compaction needs the runtime to update every pointer to the moved objects, which is why only managed runtimes with a precise GC can do it. Pinned objects (for example, buffers passed to native I/O) block it.",
            s =>
            {
                s.Remove("req");
                var live = segments.Where(x => x.Owner is not null).ToList();
                segments.Clear();
                var pos = 0;
                foreach (var l in live)
                {
                    segments.Add((pos, l.Size, l.Owner));
                    pos += l.Size;
                }

                segments.Add((pos, 100 - pos, null));
                Alloc("G", 18);
                Render(s, "G", Good);
                s.Say("After compaction: one big hole, G allocated", Good);
            });

        return b.Lesson("Heap fragmentation", "Allocation", "Enough free memory but no hole big enough: external fragmentation and compaction.");
    }

    // ------------------------------------------------------------------------------------------
    private static Lesson BuddyAllocator()
    {
        const double x0 = 50, unit = 14;
        var blocks = new List<(int Start, int Size, string? Owner, int Used)> { (0, 64, null, 0) };

        void Render(SceneState s, string? highlight)
        {
            s.RemoveWhere(sh => sh.Id.StartsWith("blk", StringComparison.Ordinal));
            foreach (var (start, size, owner, used) in blocks.OrderBy(x => x.Start))
            {
                var id = $"blk{start}_{size}";
                s.Box(id, owner is null ? $"free {size}" : $"{owner}: {used}/{size}", x0 + start * unit, 200, size * unit - 3, 110, $"@{start}");
                if (owner is null)
                {
                    s.Ghost(id);
                }
                else
                {
                    s.Tint(id, owner[0] - 'A').FillTo(id, used / (double)size);
                    if (owner == highlight)
                    {
                        s.Paint(id, Info).Tint(id, -1);
                    }
                }
            }
        }

        string Alloc(string name, int request)
        {
            var size = 1;
            while (size < request)
            {
                size *= 2;
            }

            var splits = new List<int>();
            var idx = blocks.FindIndex(x => x.Owner is null && x.Size == size);
            while (idx < 0)
            {
                var bigger = blocks.Where(x => x.Owner is null && x.Size > size).OrderBy(x => x.Size).ThenBy(x => x.Start).First();
                var bi = blocks.IndexOf(bigger);
                var half = bigger.Size / 2;
                blocks[bi] = (bigger.Start, half, null, 0);
                blocks.Insert(bi + 1, (bigger.Start + half, half, null, 0));
                splits.Add(bigger.Size);
                idx = blocks.FindIndex(x => x.Owner is null && x.Size == size);
            }

            blocks[idx] = (blocks[idx].Start, size, name, request);
            return splits.Count == 0 ? "A free block of exactly that size exists, so no split is needed." : $"Split {string.Join(" -> ", splits.Append(size))} until a block of {size} exists.";
        }

        string Free(string name)
        {
            var i = blocks.FindIndex(x => x.Owner == name);
            blocks[i] = blocks[i] with { Owner = null, Used = 0 };
            var merges = new List<int>();
            var merged = true;
            while (merged)
            {
                merged = false;
                foreach (var blk in blocks.Where(x => x.Owner is null).ToList())
                {
                    var buddyStart = blk.Start ^ blk.Size;
                    var buddy = blocks.FirstOrDefault(x => x.Owner is null && x.Size == blk.Size && x.Start == buddyStart && x != blk);
                    if (buddy.Size == blk.Size && buddy.Start == buddyStart && blk.Size < 64)
                    {
                        blocks.Remove(blk);
                        blocks.Remove(buddy);
                        blocks.Add((Math.Min(blk.Start, buddy.Start), blk.Size * 2, null, 0));
                        merges.Add(blk.Size * 2);
                        merged = true;
                        break;
                    }
                }
            }

            return merges.Count == 0 ? "Its buddy is still in use, so it cannot merge yet." : $"Its buddy is free, so they merge: {string.Join(" -> ", merges)}.";
        }

        var b = new StoryBuilder("MM6", "A buddy allocator splits power-of-two blocks in halves and merges a freed block with its 'buddy' (address XOR size). Merging is O(log n) and external fragmentation stays low; the price is internal waste from rounding up.")
            .Setup(s => s.Caption("cap", "64 KB region, block sizes are powers of two", 50, 150))
            .NoCode();

        b.Step("One free 64 KB block", "Every request is rounded up to a power of two and served by splitting larger blocks in half.",
            "The Linux kernel manages physical pages with a buddy allocator (orders 0 to 10, i.e. 4 KB to 4 MB blocks). You can see its free lists in /proc/buddyinfo.",
            s => Render(s, null));

        foreach (var (name, req) in new[] { ("A", 7), ("B", 14), ("C", 6) })
        {
            var text = Alloc(name, req);
            var n = name;
            b.Step($"Allocate {name} ({req} KB)", text,
                name == "A" ? "A asked for 7 but got 8. That one unused KB is internal fragmentation, shown by the partly filled bar. On average, rounding up wastes about 25%." : string.Empty,
                s => Render(s, n));
        }

        foreach (var name in new[] { "A", "C", "B" })
        {
            var text = Free(name);
            b.Step($"Free {name}", text,
                name == "C" ? "A block's buddy address is just start XOR size, so finding it is a single instruction. No list search is needed." : string.Empty,
                s => Render(s, null));
        }

        b.Step("Back to one block",
            "Every merge succeeded, so the region is whole again.",
            "Slab allocators (kmalloc) sit on top of the buddy system and carve pages into many small objects of the same size, which avoids most of the rounding waste.",
            s => s.Say("All buddies merged: 64 KB free in one piece", Good));

        return b.Lesson("Buddy allocator", "Allocation", "Power-of-two splitting and merging, as used by the Linux kernel for physical pages.");
    }

    // ------------------------------------------------------------------------------------------
    private static Lesson GarbageCollection()
    {
        (string Id, string Text, double X, double Y)[] objects =
        [
            ("o1", "Order", 360, 80), ("o2", "Customer", 560, 80), ("o3", "Address", 760, 80),
            ("o4", "Cache", 360, 220), ("o5", "Entry", 560, 220),
            ("o6", "Temp A", 360, 360), ("o7", "Temp B", 560, 360), ("o8", "byte[]", 760, 360)
        ];

        return new StoryBuilder("MM7", "A tracing GC finds everything reachable from the roots, frees the rest, and compacts the survivors. Generations make this cheap, because most objects die young.")
            .Setup(s =>
            {
                s.Region("roots", "roots", 30, 40, 220, 360, Info);
                s.Box("r1", "stack: order", 50, 90, 180, 46, tone: Info);
                s.Box("r2", "static: Cache", 50, 230, 180, 46, tone: Info);
                s.Region("heap", "managed heap", 290, 40, 680, 400);
                foreach (var (id, text, x, y) in objects)
                {
                    s.Box(id, text, x, y, 150, 50);
                }

                s.Connect("r1", "o1").Connect("o1", "o2").Connect("o2", "o3");
                s.Connect("r2", "o4").Connect("o4", "o5");
                s.Connect("o6", "o7").Connect("o7", "o6");
            })
            .NoCode()
            .Step("Objects and references",
                "Roots are references the program can use directly: locals on stacks, static fields, CPU registers. Objects reference each other.",
                "Allocation just bumps a pointer, so the heap fills from left to right. A GC is triggered when the gen0 budget (a few MB, tuned dynamically) is used up.")
            .Step("Mark: trace from the roots",
                "Starting at each root, follow every reference and mark what is reached. Order, Customer, Address, Cache and Entry are alive.",
                "Marking is a graph traversal (BFS or DFS) over live objects only, so its cost depends on what is alive, not on how much garbage there is. Managed threads are paused at safe points so the graph does not change underneath.",
                s =>
                {
                    s.PaintAll(Good, "o1", "o2", "o3", "o4", "o5");
                    s.RetoneLinks(Good, l => l.From.StartsWith('r') || l.From is "o1" or "o2" or "o4");
                })
            .Step("Sweep: unreachable objects are garbage",
                "Temp A, Temp B and the byte[] were never reached. Temp A and Temp B reference each other, but that cycle does not keep them alive.",
                "Reference counting would leak this cycle forever. A tracing GC handles cycles naturally because only reachability from roots matters.",
                s =>
                {
                    s.PaintAll(Bad, "o6", "o7", "o8");
                    s.Ghost("o6").Ghost("o7").Ghost("o8");
                })
            .Step("Compact: slide survivors together",
                "Live objects are moved next to each other and every reference to them is updated. The free space becomes one contiguous block again.",
                "Compaction removes fragmentation and keeps allocation a simple pointer bump. The cost is copying and fixing pointers, and pinned objects (fixed or GCHandle.Pinned) cannot move.",
                s =>
                {
                    s.Remove("o6", "o7", "o8");
                    s.Place("o1", 360, 80).Place("o2", 520, 80).Place("o3", 680, 80).Place("o4", 360, 150).Place("o5", 520, 150);
                    s.Box("free", "free space: allocation continues here", 360, 240, 560, 60, tone: Good);
                    s.Ghost("free");
                })
            .Step("Generations: most objects die young",
                "Survivors are promoted from gen0 to gen1 and then gen2. Gen0 collections only scan young objects, so they take milliseconds or less.",
                "A write barrier records old-to-young references in a card table, so a gen0 GC does not have to scan the whole old heap. Full gen2 collections are rare and expensive. Avoid GC.Collect() and mid-life objects that survive gen0 and then die in gen2.",
                s =>
                {
                    s.Remove("free");
                    s.Region("g0", "gen0: new objects", 300, 330, 210, 100, Info);
                    s.Region("g1", "gen1", 530, 330, 180, 100, Warn);
                    s.Region("g2", "gen2: long-lived", 730, 330, 230, 100, Good);
                    s.Box("young", "new temps", 320, 370, 170, 44, tone: Info);
                    s.Move("shape:o4", "shape:g2", "promoted", Good);
                    s.Say("Most objects die in gen0, which is cheap to collect", Good);
                })
            .Lesson("Garbage collection: mark, sweep, compact, generations", "Garbage collection", "How a tracing GC decides what is alive, and why generations make it fast.");
    }

    // ------------------------------------------------------------------------------------------
    private static Lesson ManagedLeaks() =>
        new StoryBuilder("MM8", "A GC can only free what is unreachable. Event subscriptions, static caches, timers and captured closures keep objects reachable long after you are done with them.")
            .Setup(s =>
            {
                s.Box("svc", "PriceService (singleton)", 380, 40, 240, 60, "event PriceChanged", Info);
                s.Box("mem", "memory", 40, 480, 920, 40);
                s.FillTo("mem", 0.15);
            })
            .Code("""
                class PriceView : IDisposable
                {
                    public PriceView(PriceService svc)
                    {
                        _svc = svc;
                        _svc.PriceChanged += OnPriceChanged;     // svc now references this view
                    }
                    public void Dispose() => _svc.PriceChanged -= OnPriceChanged;   // the fix
                }
                """)
            .Step("Views subscribe to a long-lived event",
                "Each open view subscribes to the singleton's PriceChanged event.",
                "'+=' stores a delegate that holds a reference to the subscriber (its 'this'). The publisher now references every subscriber.",
                [6],
                s =>
                {
                    for (var i = 0; i < 3; i++)
                    {
                        s.Box("v" + i, $"PriceView #{i + 1}", 120 + i * 270, 200, 200, 56, "open", Good);
                        s.Connect("svc", "v" + i, "delegate");
                    }
                })
            .Step("Views are closed but never unsubscribe",
                "The user closes the views. Your code drops its references, but the event's delegate list still points at them.",
                "The views are reachable from a root (the static singleton), so the GC must keep them, and everything they reference: controls, bitmaps, data.",
                [6],
                s =>
                {
                    for (var i = 0; i < 3; i++)
                    {
                        s.Paint("v" + i, Warn).SubText("v" + i, "closed, still referenced");
                    }

                    s.FillTo("mem", 0.35);
                })
            .Step("Hours later",
                "Every view ever opened is still in memory. Memory climbs, gen2 collections run more often, and the process eventually hits OutOfMemory.",
                "Symptoms: steadily growing private bytes, longer and more frequent full GCs, and high CPU in the GC. A heap snapshot (dotnet-gcdump, Visual Studio, PerfView) shows the 'path to root' that keeps the objects alive.",
                [],
                s =>
                {
                    for (var i = 3; i < 9; i++)
                    {
                        s.Box("v" + i, $"#{i + 1}", 60 + (i - 3) * 150, 330, 130, 46, "closed", Bad);
                        s.Connect("svc", "v" + i);
                    }

                    for (var i = 0; i < 3; i++)
                    {
                        s.Paint("v" + i, Bad);
                    }

                    s.FillTo("mem", 0.92).Paint("mem", Bad);
                    s.Say("Leak: closed views pinned by the singleton's event", Bad);
                })
            .Step("Fix: unsubscribe in Dispose",
                "Remove the handler when the view closes. Once it is no longer reachable, the GC collects it.",
                "Other common leaks: static dictionaries used as caches without eviction (use MemoryCache with limits), System.Threading.Timer callbacks, long-lived closures capturing big objects, and forgetting to dispose CancellationTokenRegistration. Weak events are another option.",
                [8],
                s =>
                {
                    s.ClearLinks();
                    s.RemoveWhere(sh => sh.Id.StartsWith('v'));
                    s.FillTo("mem", 0.15).Paint("mem", Good);
                    s.Say("Unsubscribed: views become garbage and are collected", Good);
                })
            .Lesson("Memory leaks in managed code", "Garbage collection", "Why a garbage-collected app can still leak, and how to find the reference that holds on.");

    // ------------------------------------------------------------------------------------------
    private static Lesson CacheLocality()
    {
        static string Id(int r, int c) => $"m{r}{c}";

        return new StoryBuilder("MM9", "Memory is fetched in 64-byte cache lines. Walk data in the order it is laid out (row by row for C#/C arrays), and every fetched line is fully used.")
            .Setup(s =>
            {
                s.Caption("mcap", "int[4,4] as you think of it", 40, 30);
                for (var r = 0; r < 4; r++)
                {
                    for (var c = 0; c < 4; c++)
                    {
                        s.Box(Id(r, c), $"{r},{c}", 40 + c * 70, 60 + r * 62, 62, 52);
                    }
                }

                s.Caption("lcap", "how it is laid out in memory (row-major); pretend one cache line holds 4 ints", 340, 30, 640);
                for (var i = 0; i < 16; i++)
                {
                    s.Box("a" + i, $"{i / 4},{i % 4}", 340 + i * 39, 70, 36, 46);
                    s.Shape("a" + i).FontSize = 10.5;
                }

                for (var l = 0; l < 4; l++)
                {
                    s.Region("line" + l, $"line {l}", 336 + l * 156, 50, 154, 74);
                }

                s.Label("stats", "", 40, 330, 900, 16);
            })
            .Code("""
                for (int r = 0; r < n; r++)            // fast: walks memory in order
                    for (int c = 0; c < n; c++)
                        sum += m[r, c];

                for (int c = 0; c < n; c++)            // slow: jumps n * 4 bytes every access
                    for (int r = 0; r < n; r++)
                        sum += m[r, c];
                """)
            .Step("Rows are contiguous",
                "A C# rectangular array stores row 0, then row 1, and so on. Neighbours in a row are neighbours in memory.",
                "Real lines hold 16 ints (64 bytes); 4 are shown here to keep the picture small. The memory system always moves whole lines, never single ints.",
                [1, 2, 3])
            .Step("Row-by-row traversal",
                "Reading (0,0) misses and brings in line 0. The next three reads hit. One miss per line.",
                "The prefetcher also spots the sequential stream and fetches the next lines before you need them. This loop runs at close to full memory bandwidth.",
                [1, 2, 3],
                s =>
                {
                    for (var c = 0; c < 4; c++)
                    {
                        s.Paint(Id(0, c), c == 0 ? Warn : Good).Paint("a" + c, c == 0 ? Warn : Good);
                    }

                    s.Paint("line0", Good);
                    s.Write("stats", "row order: 1 miss + 3 hits per line, 75% hit rate (94% with real 64-byte lines)");
                })
            .Step("Column-by-column traversal",
                "Reading down a column jumps a whole row each time: (0,0), (1,0), (2,0), (3,0) are in four different lines. Every access misses.",
                "For a 4096 x 4096 int matrix, each column step jumps 16 KB. Lines are evicted before you return for their other values, so the column loop is often 5-20x slower for the same arithmetic.",
                [5, 6, 7],
                s =>
                {
                    for (var i = 0; i < 16; i++)
                    {
                        s.Paint("a" + i, Neutral);
                    }

                    for (var r = 0; r < 4; r++)
                    {
                        for (var c = 0; c < 4; c++)
                        {
                            s.Paint(Id(r, c), Neutral);
                        }

                        s.Paint(Id(r, 0), Bad).Paint("a" + (r * 4), Bad).Paint("line" + r, Bad);
                    }

                    s.Write("stats", "column order: every access is a miss, 0% hit rate on large matrices");
                })
            .Step("Fixes: match the layout",
                "Swap the loops, transpose the data, or process in tiles that fit in the cache (blocking).",
                "Matrix multiplication libraries tile into L1/L2-sized blocks. Data-oriented design prefers struct-of-arrays over array-of-structs when a loop needs only one field. Jagged arrays (int[][]) add a pointer hop per row.",
                [],
                s =>
                {
                    for (var r = 0; r < 4; r++)
                    {
                        s.Paint("line" + r, Good);
                        for (var c = 0; c < 4; c++)
                        {
                            s.Paint(Id(r, c), Good).Paint("a" + (r * 4 + c), Good);
                        }
                    }

                    s.Write("stats", "same work, same Big-O: the access order decides the speed");
                    s.Say("Iterate in memory order", Good);
                })
            .Lesson("Cache locality: row vs column order", "Caches", "Same loop, same Big-O, 10x different speed: why access order matters.");
    }

    // ------------------------------------------------------------------------------------------
    private static Lesson FalseSharing() =>
        new StoryBuilder("MM10", "Two threads writing different variables on the same cache line fight over that line as if they were sharing data. Pad hot per-thread data onto separate lines.")
            .Setup(s =>
            {
                s.Box("c0", "core 0", 60, 60, 300, 70, "thread 1: countA++", Info);
                s.Box("c1", "core 1", 640, 60, 300, 70, "thread 2: countB++", Info);
                s.Region("line", "one 64-byte cache line", 280, 300, 440, 120, Warn);
                s.Box("ca", "countA", 310, 345, 180, 56);
                s.Box("cb", "countB", 510, 345, 180, 56);
                s.Tint("ca", 0).Tint("cb", 1);
            })
            .Code("""
                class Counters { public long A; public long B; }    // adjacent: same cache line

                Parallel.Invoke(
                    () => { for (int i = 0; i < N; i++) c.A++; },
                    () => { for (int i = 0; i < N; i++) c.B++; });

                [StructLayout(LayoutKind.Explicit, Size = 128)]
                struct Padded { [FieldOffset(0)] public long Value; }  // fix: one counter per line
                """)
            .Step("Two independent counters",
                "Thread 1 only touches countA and thread 2 only touches countB. Logically, nothing is shared.",
                "There is no lock and no race on the values, so the code is correct. The problem is purely about performance.",
                [1, 3, 4, 5])
            .Step("But they share a cache line",
                "The two fields sit next to each other, so they land in the same 64-byte line. Caches track ownership per line, not per variable.",
                "Coherence protocols (MESI) give one core exclusive ownership of a line before it may write. Both cores want to write to this line.",
                [1],
                s => s.Paint("line", Bad))
            .Step("Core 0 writes: the line moves to core 0",
                "To increment countA, core 0 needs the line in Modified state, so core 1's copy is invalidated.",
                "This costs a request on the interconnect, about 40-100 cycles, instead of a 1-cycle L1 hit.",
                [4],
                s =>
                {
                    s.Paint("c0", Good).Paint("c1", Warn);
                    s.Move("shape:line", "shape:c0", "line -> core 0 (M)", Warn);
                })
            .Step("Core 1 writes: the line moves back",
                "Now core 1 increments countB and takes the line back. They take turns, millions of times per second.",
                "This ping-pong can make two threads slower than one. Profilers show it as 'HITM' (hit-modified) events or high cache-to-cache transfer counts.",
                [5],
                s =>
                {
                    s.Paint("c0", Warn).Paint("c1", Good);
                    s.Move("shape:line", "shape:c1", "line -> core 1 (M)", Warn);
                    s.Move("shape:c1", "shape:c0", "invalidate", Bad);
                    s.Say("False sharing: the line ping-pongs between cores", Bad);
                })
            .Step("Fix: give each counter its own line",
                "Pad each counter to 64 (or 128) bytes. Now each core keeps its own line in Modified state and increments hit L1 every time.",
                "128 bytes covers adjacent-line prefetching on Intel CPUs. .NET uses this padding inside ConcurrentQueue, and per-thread or per-core counters merged at the end (the striped-counter pattern) avoid the problem entirely.",
                [7, 8],
                s =>
                {
                    s.Remove("line");
                    s.Region("lineA", "line 1", 100, 300, 300, 120, Good);
                    s.Region("lineB", "line 2", 600, 300, 300, 120, Good);
                    s.Place("ca", 160, 345).Place("cb", 660, 345);
                    s.PaintAll(Good, "c0", "c1");
                    s.Say("Separate lines: each core keeps its own line, near-linear scaling", Good);
                })
            .Lesson("False sharing", "Caches", "Why two threads touching different variables can still slow each other down.");
}
