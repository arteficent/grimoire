// Generated from the app's curriculum by tests/grimoire.Tests. Do not edit by hand:
// set GRIMOIRE_UPDATE_CATALOG=1 and run `dotnet test` to regenerate.
window.GRIMOIRE_CATALOG = {
  "lessonCount": 147,
  "stepCount": 861,
  "tracks": [
    {
      "id": "dt",
      "title": "Data Representation",
      "tagline": "Bits, hex, integers, floats, text encodings, struct layout and what a memory address means.",
      "accent": "#A3E635",
      "monogram": "DR",
      "lessons": [
        {
          "id": "DR1",
          "title": "Bits, bytes and hexadecimal",
          "section": "Numbers and memory",
          "summary": "Why every byte is two hex digits, and how to convert between binary, hex and decimal in your head.",
          "steps": 5,
          "runnable": false
        },
        {
          "id": "DR2",
          "title": "What a hexadecimal memory address means",
          "section": "Numbers and memory",
          "summary": "Reading hex dumps, 64-bit addresses, page offsets, alignment and pointer arithmetic.",
          "steps": 5,
          "runnable": false
        },
        {
          "id": "DR3",
          "title": "Integers: unsigned, two's complement and overflow",
          "section": "Numbers and memory",
          "summary": "How signed numbers are stored, why overflow wraps, and every integer type side by side.",
          "steps": 5,
          "runnable": false
        },
        {
          "id": "DR4",
          "title": "Endianness: byte order in memory",
          "section": "Numbers and memory",
          "summary": "Little- vs big-endian, network byte order, and the bug you get when they are mixed up.",
          "steps": 4,
          "runnable": false
        },
        {
          "id": "DR5",
          "title": "Floating point (IEEE 754)",
          "section": "Numbers and memory",
          "summary": "Sign, exponent, mantissa; why 0.1 + 0.2 != 0.3; NaN and Infinity; FP64 to FP4.",
          "steps": 5,
          "runnable": false
        },
        {
          "id": "DR6",
          "title": "Characters and strings: ASCII, Unicode, UTF-8, UTF-16",
          "section": "Text",
          "summary": "Code points, variable-length encodings, surrogate pairs and how strings sit in memory.",
          "steps": 4,
          "runnable": false
        },
        {
          "id": "DR7",
          "title": "Booleans, enums, flags and bit fields",
          "section": "Numbers and memory",
          "summary": "How yes/no values and options are packed into bits and tested with &, | and ~.",
          "steps": 4,
          "runnable": false
        },
        {
          "id": "DR8",
          "title": "Structs, alignment and padding",
          "section": "Layout",
          "summary": "Why field order changes struct size, and when to pack.",
          "steps": 4,
          "runnable": false
        },
        {
          "id": "DR9",
          "title": "References, pointers and .NET object layout",
          "section": "Layout",
          "summary": "What a reference holds, how objects are laid out, value vs reference types, and boxing.",
          "steps": 4,
          "runnable": false
        }
      ]
    },
    {
      "id": "ar",
      "title": "Processors & Instruction Sets",
      "tagline": "x86, ARM, RISC-V and GPU ISAs: decoding, pipelines, out-of-order, SIMD and branch prediction.",
      "accent": "#F472B6",
      "monogram": "IS",
      "lessons": [
        {
          "id": "AR1",
          "title": "What an instruction set architecture is",
          "section": "Instruction sets",
          "summary": "The software-hardware contract, CISC vs RISC, and the same code in x86, ARM and RISC-V.",
          "steps": 5,
          "runnable": false
        },
        {
          "id": "AR2",
          "title": "Decoding a RISC-V instruction, bit by bit",
          "section": "Instruction sets",
          "summary": "From four bytes in memory to 'add a0, a1, a2': fields, opcodes and register reads.",
          "steps": 5,
          "runnable": false
        },
        {
          "id": "AR3",
          "title": "The instruction pipeline and its hazards",
          "section": "Execution",
          "summary": "IF-ID-EX-MEM-WB animated: overlap, forwarding, load stalls and branch flushes.",
          "steps": 5,
          "runnable": false
        },
        {
          "id": "AR4",
          "title": "x86-64: CISC bytes to out-of-order micro-ops",
          "section": "CPU microarchitecture",
          "summary": "Decoding, register renaming, execution ports and in-order retirement, animated.",
          "steps": 6,
          "runnable": false
        },
        {
          "id": "AR5",
          "title": "ARM AArch64: a load/store architecture",
          "section": "Instruction sets",
          "summary": "Why ARM splits read-modify-write into load, add, store, and why that enables wide, efficient cores.",
          "steps": 5,
          "runnable": false
        },
        {
          "id": "AR6",
          "title": "SIMD: one instruction, many numbers",
          "section": "Execution",
          "summary": "Vector registers and instructions across x86, ARM and RISC-V, and how .NET uses them.",
          "steps": 4,
          "runnable": false
        },
        {
          "id": "AR7",
          "title": "Branch prediction and speculative execution",
          "section": "CPU microarchitecture",
          "summary": "Saturating counters, misprediction cost, sorted-array speedups and Spectre.",
          "steps": 7,
          "runnable": false
        },
        {
          "id": "AR8",
          "title": "CPU designs: hybrid cores, chiplets, SoCs, many-core",
          "section": "CPU microarchitecture",
          "summary": "How modern CPUs are organised, and why topology now matters as much as clock speed.",
          "steps": 5,
          "runnable": false
        },
        {
          "id": "AR9",
          "title": "GPU instruction sets: PTX, SASS, AMDGPU, SPIR-V",
          "section": "GPU execution",
          "summary": "Why GPUs ship virtual ISAs, and how drivers turn them into native machine code.",
          "steps": 4,
          "runnable": false
        },
        {
          "id": "AR10",
          "title": "How a GPU executes an instruction",
          "section": "GPU execution",
          "summary": "Warp scheduling, operand collection, 32-lane execution, masks and tensor cores.",
          "steps": 5,
          "runnable": false
        }
      ]
    },
    {
      "id": "mm",
      "title": "Memory Management",
      "tagline": "Stack, heap, virtual memory, paging, allocators, garbage collection and caches.",
      "accent": "#C084FC",
      "monogram": "MM",
      "lessons": [
        {
          "id": "MM1",
          "title": "Stack vs heap",
          "section": "Allocation",
          "summary": "Where values live, why stack memory is free, and what the GC must clean up.",
          "steps": 5,
          "runnable": false
        },
        {
          "id": "MM2",
          "title": "Virtual memory, page tables and the TLB",
          "section": "Virtual memory",
          "summary": "How every address is translated, what a TLB miss costs, and what a page fault really is.",
          "steps": 6,
          "runnable": false
        },
        {
          "id": "MM3",
          "title": "Page replacement: FIFO vs LRU",
          "section": "Virtual memory",
          "summary": "Which page to evict when RAM is full, run side by side on the same accesses.",
          "steps": 12,
          "runnable": false
        },
        {
          "id": "MM4",
          "title": "Thrashing",
          "section": "Virtual memory",
          "summary": "Why adding work past a memory limit makes a system slower, not faster.",
          "steps": 4,
          "runnable": false
        },
        {
          "id": "MM5",
          "title": "Heap fragmentation",
          "section": "Allocation",
          "summary": "Enough free memory but no hole big enough: external fragmentation and compaction.",
          "steps": 11,
          "runnable": false
        },
        {
          "id": "MM6",
          "title": "Buddy allocator",
          "section": "Allocation",
          "summary": "Power-of-two splitting and merging, as used by the Linux kernel for physical pages.",
          "steps": 8,
          "runnable": false
        },
        {
          "id": "MM7",
          "title": "Garbage collection: mark, sweep, compact, generations",
          "section": "Garbage collection",
          "summary": "How a tracing GC decides what is alive, and why generations make it fast.",
          "steps": 5,
          "runnable": false
        },
        {
          "id": "MM8",
          "title": "Memory leaks in managed code",
          "section": "Garbage collection",
          "summary": "Why a garbage-collected app can still leak, and how to find the reference that holds on.",
          "steps": 4,
          "runnable": false
        },
        {
          "id": "MM9",
          "title": "Cache locality: row vs column order",
          "section": "Caches",
          "summary": "Same loop, same Big-O, 10x different speed: why access order matters.",
          "steps": 4,
          "runnable": false
        },
        {
          "id": "MM10",
          "title": "False sharing",
          "section": "Caches",
          "summary": "Why two threads touching different variables can still slow each other down.",
          "steps": 5,
          "runnable": false
        }
      ]
    },
    {
      "id": "os",
      "title": "Operating Systems",
      "tagline": "Processes, system calls, scheduling and classic deadlock problems.",
      "accent": "#FBBF24",
      "monogram": "OS",
      "lessons": [
        {
          "id": "OS1",
          "title": "Processes, threads and the context switch",
          "section": "Processes",
          "summary": "What really happens when the OS swaps one thread for another, and why it costs more than you think.",
          "steps": 6,
          "runnable": false
        },
        {
          "id": "OS2",
          "title": "System calls: crossing into the kernel",
          "section": "Processes",
          "summary": "The path of a file read from your code through the kernel to the SSD and back.",
          "steps": 5,
          "runnable": false
        },
        {
          "id": "OS3",
          "title": "CPU scheduling: FCFS, SJF, SRTF, round robin",
          "section": "Scheduling",
          "summary": "The same four jobs under four schedulers, and the waiting time each produces.",
          "steps": 5,
          "runnable": false
        },
        {
          "id": "OS4",
          "title": "Priority inversion (the Mars Pathfinder bug)",
          "section": "Scheduling",
          "summary": "How a medium-priority task can starve a high-priority one, and how priority inheritance fixes it.",
          "steps": 5,
          "runnable": false
        },
        {
          "id": "OS5",
          "title": "Dining philosophers",
          "section": "Deadlock",
          "summary": "The classic deadlock puzzle, and two standard ways to make it impossible.",
          "steps": 5,
          "runnable": false
        },
        {
          "id": "OS6",
          "title": "Banker's algorithm (deadlock avoidance)",
          "section": "Deadlock",
          "summary": "Granting resources only when a safe completion order still exists.",
          "steps": 10,
          "runnable": false
        }
      ]
    },
    {
      "id": "cc",
      "title": "Concurrency & Locks",
      "tagline": "Deadlocks, races, memory visibility and the primitives that tame them.",
      "accent": "#F87171",
      "monogram": "CC",
      "lessons": [
        {
          "id": "D1",
          "title": "Lock-ordering (AB-BA) deadlock",
          "section": "Deadlocks and how to avoid them",
          "summary": "Thread 1 takes lock A then B, thread 2 takes lock B then A. Both hold one lock and wait forever for the other.",
          "steps": 8,
          "runnable": true
        },
        {
          "id": "D2",
          "title": "Fix by lock ordering (breaks circular wait)",
          "section": "Deadlocks and how to avoid them",
          "summary": "Both threads acquire the two locks in the same globally agreed order, derived here from a stable rank on each lock.",
          "steps": 7,
          "runnable": true
        },
        {
          "id": "D3",
          "title": "Fix by Monitor.TryEnter + back-off (breaks hold-and-wait)",
          "section": "Deadlocks and how to avoid them",
          "summary": "Each thread takes the first lock, then tries the second with a timeout. On failure it releases everything, sleeps a random interval and retries.",
          "steps": 8,
          "runnable": true
        },
        {
          "id": "D4",
          "title": "Sync-over-async deadlock (.Result on a captured context)",
          "section": "Deadlocks and how to avoid them",
          "summary": "A single-threaded SynchronizationContext (UI / classic ASP.NET) plus .Result: the continuation is queued to the very thread that is blocked waiting for it.",
          "steps": 7,
          "runnable": true
        },
        {
          "id": "D5",
          "title": "Worker-pool starvation deadlock",
          "section": "Deadlocks and how to avoid them",
          "summary": "A pool with two workers. Each queued item blocks waiting for another item that is still in the queue, so no worker is left to run it.",
          "steps": 8,
          "runnable": true
        },
        {
          "id": "D6",
          "title": "Self-deadlock: reentrant lock vs non-reentrant SemaphoreSlim",
          "section": "Deadlocks and how to avoid them",
          "summary": "Monitor (lock) is reentrant - the owning thread may re-acquire it. SemaphoreSlim(1,1) is not: a recursive call blocks on itself forever.",
          "steps": 7,
          "runnable": true
        },
        {
          "id": "D7",
          "title": "Lost wake-up (Monitor.Wait without a predicate loop)",
          "section": "Deadlocks and how to avoid them",
          "summary": "Monitor.Pulse is not sticky. If the producer signals before the consumer calls Wait, the signal is gone and the consumer blocks forever.",
          "steps": 8,
          "runnable": true
        },
        {
          "id": "D8",
          "title": "Read-to-write upgrade: exception, lost update, or upgradeable lock",
          "section": "Deadlocks and how to avoid them",
          "summary": "Two readers both asking for the write lock is a textbook deadlock, so ReaderWriterLockSlim refuses it outright. The naive workaround then silently loses updates.",
          "steps": 9,
          "runnable": true
        },
        {
          "id": "S1",
          "title": "Lost updates: ++ vs Interlocked vs lock",
          "section": "Synchronization primitives",
          "summary": "counter++ compiles to read / add / write. Without synchronisation concurrent increments overwrite each other.",
          "steps": 8,
          "runnable": true
        },
        {
          "id": "S2",
          "title": "Primitive tour: lock, SemaphoreSlim, ReaderWriterLockSlim, SpinLock, Interlocked",
          "section": "Synchronization primitives",
          "summary": "A read-heavy shared counter guarded by each primitive in turn, so the cost and the semantics can be compared side by side.",
          "steps": 6,
          "runnable": true
        },
        {
          "id": "S3",
          "title": "Coordination: ManualResetEventSlim, CountdownEvent, Barrier",
          "section": "Synchronization primitives",
          "summary": "Three phases of workers coordinated by a start gate, a completion latch and a phased barrier.",
          "steps": 6,
          "runnable": true
        },
        {
          "id": "S4",
          "title": "Memory visibility: a non-volatile stop flag",
          "section": "Synchronization primitives",
          "summary": "A spin loop over a plain bool may never observe another thread's write: the JIT is free to hoist the read out of the loop.",
          "steps": 6,
          "runnable": true
        },
        {
          "id": "S5",
          "title": "Async-friendly mutual exclusion (SemaphoreSlim.WaitAsync)",
          "section": "Synchronization primitives",
          "summary": "You cannot await inside a lock block. SemaphoreSlim.WaitAsync gives mutual exclusion without blocking a thread, and supports timeout/cancellation.",
          "steps": 6,
          "runnable": true
        },
        {
          "id": "C1",
          "title": "Thread-unsafe vs thread-safe resource (List / Dictionary / Queue)",
          "section": "Thread-safe collections",
          "summary": "List<T>.Add is read index, write slot, increment count. Concurrently that loses items, writes nulls into the backing array, or throws from a mid-flight resize.",
          "steps": 5,
          "runnable": true
        },
        {
          "id": "C2",
          "title": "Dictionary corruption vs ConcurrentDictionary",
          "section": "Thread-safe collections",
          "summary": "A racing resize can leave the bucket/entry arrays inconsistent. Historically that turned a lookup into an infinite loop - today it usually throws or loses entries.",
          "steps": 6,
          "runnable": true
        },
        {
          "id": "C3",
          "title": "Thread-safe type, unsafe compound operation (check-then-act)",
          "section": "Thread-safe collections",
          "summary": "ContainsKey followed by Add is two atomic operations, not one. And GetOrAdd may run its value factory more than once for the same key.",
          "steps": 7,
          "runnable": true
        },
        {
          "id": "C4",
          "title": "Producer/consumer: Queue vs ConcurrentQueue vs BlockingCollection",
          "section": "Thread-safe collections",
          "summary": "Queue<T> loses and corrupts items, ConcurrentQueue<T> forces the consumer to spin, BlockingCollection<T> blocks properly - and deadlocks if you forget CompleteAdding.",
          "steps": 7,
          "runnable": true
        },
        {
          "id": "C5",
          "title": "Enumerating while writing: throw, moving snapshot, or copy-on-write",
          "section": "Thread-safe collections",
          "summary": "List<T>/Dictionary<K,V> throw InvalidOperationException when mutated during foreach; ConcurrentDictionary gives a moving view; copy-on-write gives a stable immutable snapshot.",
          "steps": 8,
          "runnable": true
        }
      ]
    },
    {
      "id": "ds",
      "title": "Data Structures",
      "tagline": "How arrays, lists, hash tables, trees, heaps and filters really work in memory.",
      "accent": "#34D399",
      "monogram": "DS",
      "lessons": [
        {
          "id": "DS1",
          "title": "Arrays vs linked lists: memory layout matters",
          "section": "Linear structures",
          "summary": "Why an O(n) array scan beats an O(n) list walk, and when insertion cost flips the answer.",
          "steps": 5,
          "runnable": false
        },
        {
          "id": "DS2",
          "title": "Stack and queue (ring buffer)",
          "section": "Linear structures",
          "summary": "LIFO, the call stack, and how a circular buffer gives O(1) FIFO without shifting.",
          "steps": 6,
          "runnable": false
        },
        {
          "id": "DS3",
          "title": "Hash table: hashing, collisions and resizing",
          "section": "Hashing",
          "summary": "How keys become array indexes, why collisions happen, and what a resize really costs.",
          "steps": 6,
          "runnable": false
        },
        {
          "id": "DS4",
          "title": "Binary search tree and balancing",
          "section": "Trees",
          "summary": "Ordered lookups in O(log n), and how sorted input silently turns a tree into a list.",
          "steps": 10,
          "runnable": false
        },
        {
          "id": "DS5",
          "title": "Binary heap and priority queues",
          "section": "Trees",
          "summary": "A tree hidden inside an array: sift-up, sift-down, and why PriorityQueue is so fast.",
          "steps": 12,
          "runnable": false
        },
        {
          "id": "DS6",
          "title": "Trie (prefix tree)",
          "section": "Trees",
          "summary": "Prefix sharing for autocomplete, spell-check and routing tables.",
          "steps": 4,
          "runnable": false
        },
        {
          "id": "DS7",
          "title": "LRU cache: hash map + doubly linked list",
          "section": "Hashing",
          "summary": "The classic O(1) cache design, with eviction step by step.",
          "steps": 8,
          "runnable": false
        },
        {
          "id": "DS8",
          "title": "Bloom filter",
          "section": "Hashing",
          "summary": "A probabilistic set that saves disk reads, with no false negatives and a few false positives.",
          "steps": 5,
          "runnable": false
        },
        {
          "id": "DS9",
          "title": "B-tree: why databases use wide nodes",
          "section": "Trees",
          "summary": "Designing for page reads: how fan-out turns 20 disk reads into 3.",
          "steps": 5,
          "runnable": false
        }
      ]
    },
    {
      "id": "al",
      "title": "Algorithms",
      "tagline": "Searching, sorting, graphs and dynamic programming, step by step.",
      "accent": "#60A5FA",
      "monogram": "AL",
      "lessons": [
        {
          "id": "AL1",
          "title": "Big-O intuition: how cost grows",
          "section": "Foundations",
          "summary": "Why O(n^2) explodes, and why constants and caches still decide the winner among equals.",
          "steps": 4,
          "runnable": false
        },
        {
          "id": "AL2",
          "title": "Binary search",
          "section": "Searching",
          "summary": "Halving the search space, the overflow bug, and what 'not found' returns.",
          "steps": 5,
          "runnable": false
        },
        {
          "id": "AL3",
          "title": "Insertion sort",
          "section": "Sorting",
          "summary": "The simple quadratic sort that every fast library sort still uses for small slices.",
          "steps": 16,
          "runnable": false
        },
        {
          "id": "AL4",
          "title": "Merge sort",
          "section": "Sorting",
          "summary": "Divide into runs, merge in order: guaranteed O(n log n), stable, and disk friendly.",
          "steps": 9,
          "runnable": false
        },
        {
          "id": "AL5",
          "title": "Quicksort",
          "section": "Sorting",
          "summary": "In-place partitioning around a pivot, and why pivot choice decides between n log n and n^2.",
          "steps": 16,
          "runnable": false
        },
        {
          "id": "AL6",
          "title": "Graph traversal: BFS and DFS",
          "section": "Graphs",
          "summary": "Queue versus stack: level-by-level exploration versus diving deep.",
          "steps": 17,
          "runnable": false
        },
        {
          "id": "AL7",
          "title": "Dijkstra's shortest path",
          "section": "Graphs",
          "summary": "Settling the closest node first: how GPS and network routing find cheapest routes.",
          "steps": 8,
          "runnable": false
        },
        {
          "id": "AL8",
          "title": "Topological sort (Kahn's algorithm)",
          "section": "Graphs",
          "summary": "Ordering builds and jobs by their dependencies, and detecting cycles.",
          "steps": 9,
          "runnable": false
        },
        {
          "id": "AL9",
          "title": "Dynamic programming: memoisation and tables",
          "section": "Techniques",
          "summary": "Turning exponential recursion into linear time by reusing sub-results.",
          "steps": 4,
          "runnable": false
        }
      ]
    },
    {
      "id": "gp",
      "title": "GPU Computing",
      "tagline": "SIMT warps, divergence, coalescing, transfers, occupancy and VRAM.",
      "accent": "#22D3EE",
      "monogram": "GP",
      "lessons": [
        {
          "id": "GP1",
          "title": "CPU vs GPU: latency vs throughput",
          "section": "Architecture",
          "summary": "Why GPUs run thousands of threads, and when moving work to one pays off.",
          "steps": 4,
          "runnable": false
        },
        {
          "id": "GP2",
          "title": "SIMT warps and branch divergence",
          "section": "Execution",
          "summary": "Why an if/else inside a warp can halve your throughput.",
          "steps": 4,
          "runnable": false
        },
        {
          "id": "GP3",
          "title": "Memory coalescing",
          "section": "Memory",
          "summary": "How warp access patterns turn into memory transactions, and why layout decides bandwidth.",
          "steps": 4,
          "runnable": false
        },
        {
          "id": "GP4",
          "title": "Host-device transfers, pinned memory and streams",
          "section": "Memory",
          "summary": "The PCIe bottleneck and how streams overlap copies with kernels.",
          "steps": 4,
          "runnable": false
        },
        {
          "id": "GP5",
          "title": "Shared memory bank conflicts",
          "section": "Memory",
          "summary": "Why a column read can be 32x slower, and the one-column padding trick.",
          "steps": 3,
          "runnable": false
        },
        {
          "id": "GP6",
          "title": "Occupancy and latency hiding",
          "section": "Execution",
          "summary": "How registers per thread limit resident warps, and why that hides or exposes memory latency.",
          "steps": 4,
          "runnable": false
        },
        {
          "id": "GP7",
          "title": "VRAM out-of-memory and fragmentation",
          "section": "Memory",
          "summary": "Why 'CUDA out of memory' can happen with plenty of free memory.",
          "steps": 4,
          "runnable": false
        },
        {
          "id": "GP8",
          "title": "Async kernel launches and synchronisation",
          "section": "Execution",
          "summary": "Why GPU timings lie, where hidden syncs come from, and why errors appear late.",
          "steps": 5,
          "runnable": false
        }
      ]
    },
    {
      "id": "sw",
      "title": "Software Design",
      "tagline": "OOP, SOLID, design patterns, clean architecture, DDD, CQRS and robust, testable code.",
      "accent": "#818CF8",
      "monogram": "SW",
      "lessons": [
        {
          "id": "SW1",
          "title": "Coupling and cohesion: the root of maintainability",
          "section": "Foundations",
          "summary": "Why tangled modules make every change expensive, and how grouping and narrow interfaces fix it.",
          "steps": 6,
          "runnable": false
        },
        {
          "id": "SW2",
          "title": "Object-oriented programming: the four pillars",
          "section": "Object-oriented design",
          "summary": "Encapsulation, abstraction, inheritance and polymorphism, and why composition often beats inheritance.",
          "steps": 5,
          "runnable": false
        },
        {
          "id": "SW3",
          "title": "SOLID 1: single responsibility and open/closed",
          "section": "SOLID",
          "summary": "Splitting a god class by who asks for changes, and extending behaviour without editing working code.",
          "steps": 4,
          "runnable": false
        },
        {
          "id": "SW4",
          "title": "SOLID 2: Liskov substitution and interface segregation",
          "section": "SOLID",
          "summary": "The Rectangle/Square trap, honouring contracts, and splitting fat interfaces into roles.",
          "steps": 5,
          "runnable": false
        },
        {
          "id": "SW5",
          "title": "SOLID 3: dependency inversion and injection",
          "section": "SOLID",
          "summary": "Making business rules independent of databases, wiring with a DI container, and testing with fakes.",
          "steps": 5,
          "runnable": false
        },
        {
          "id": "SW6",
          "title": "Creational patterns: factory, abstract factory, builder, singleton",
          "section": "Design patterns",
          "summary": "Who decides which class is created, how complex objects are assembled, and the trouble with global singletons.",
          "steps": 4,
          "runnable": false
        },
        {
          "id": "SW7",
          "title": "Structural patterns: adapter, decorator, proxy, facade, composite",
          "section": "Design patterns",
          "summary": "Fitting mismatched interfaces, layering behaviour by wrapping, and simplifying complex subsystems.",
          "steps": 5,
          "runnable": false
        },
        {
          "id": "SW8",
          "title": "Behavioural patterns: strategy, observer, command",
          "section": "Design patterns",
          "summary": "Swappable algorithms, publish/subscribe notifications and the leak they can cause, and undoable commands.",
          "steps": 6,
          "runnable": false
        },
        {
          "id": "SW9",
          "title": "Behavioural patterns: state machine, chain of responsibility, template method",
          "section": "Design patterns",
          "summary": "Order lifecycles that reject illegal moves, middleware pipelines, and algorithm skeletons.",
          "steps": 6,
          "runnable": false
        },
        {
          "id": "SW10",
          "title": "Clean and hexagonal architecture",
          "section": "Architecture",
          "summary": "The dependency rule, ports and adapters, and keeping business rules independent of frameworks.",
          "steps": 4,
          "runnable": false
        },
        {
          "id": "SW11",
          "title": "Domain-driven design: contexts, aggregates, value objects, events",
          "section": "Architecture",
          "summary": "Modelling the business in code: bounded contexts, consistency boundaries and domain events.",
          "steps": 5,
          "runnable": false
        },
        {
          "id": "SW12",
          "title": "CQRS and event sourcing",
          "section": "Architecture",
          "summary": "Separate read and write models, storing events instead of state, replay and time travel.",
          "steps": 4,
          "runnable": false
        },
        {
          "id": "SW13",
          "title": "Monolith, modular monolith, microservices",
          "section": "Architecture",
          "summary": "What each architecture costs and buys, Conway's law, and the distributed-systems tax.",
          "steps": 5,
          "runnable": false
        },
        {
          "id": "SW14",
          "title": "Robust code: strong types, invalid states, immutability, boundaries",
          "section": "Quality",
          "summary": "Design techniques that make whole classes of bugs impossible to write.",
          "steps": 7,
          "runnable": false
        },
        {
          "id": "SW15",
          "title": "Testing: pyramid, test doubles and TDD",
          "section": "Quality",
          "summary": "How fast, isolated tests make refactoring safe, and how test-first shapes better design.",
          "steps": 5,
          "runnable": false
        }
      ]
    },
    {
      "id": "sy",
      "title": "System Design",
      "tagline": "REST, ACID, isolation, CAP, replication, sharding, caching, queues, sagas, Raft and observability.",
      "accent": "#2DD4BF",
      "monogram": "SY",
      "lessons": [
        {
          "id": "SY1",
          "title": "REST and HTTP API design",
          "section": "APIs",
          "summary": "Resources and verbs, status codes, statelessness, idempotency keys and API hygiene.",
          "steps": 6,
          "runnable": false
        },
        {
          "id": "SY2",
          "title": "API styles: REST, GraphQL, gRPC, WebSockets, webhooks",
          "section": "APIs",
          "summary": "Over-fetching, typed binary contracts, streaming and server push, and when to use each.",
          "steps": 5,
          "runnable": false
        },
        {
          "id": "SY3",
          "title": "ACID transactions and the write-ahead log",
          "section": "Data and consistency",
          "summary": "All-or-nothing transfers, crash recovery from the log, and what each ACID letter means.",
          "steps": 5,
          "runnable": false
        },
        {
          "id": "SY4",
          "title": "Isolation levels, anomalies and MVCC",
          "section": "Data and consistency",
          "summary": "Lost updates, dirty and non-repeatable reads, snapshots, and choosing an isolation level.",
          "steps": 5,
          "runnable": false
        },
        {
          "id": "SY5",
          "title": "CAP, quorums and consistency models",
          "section": "Data and consistency",
          "summary": "What replicas do during a partition, quorum reads and writes, and the consistency spectrum.",
          "steps": 6,
          "runnable": false
        },
        {
          "id": "SY6",
          "title": "Replication, lag and failover",
          "section": "Distributed data",
          "summary": "Leader-follower replication, read-your-writes, sync vs async, failover, split brain and CRDTs.",
          "steps": 5,
          "runnable": false
        },
        {
          "id": "SY7",
          "title": "Sharding and consistent hashing",
          "section": "Distributed data",
          "summary": "Hash-mod-N vs a hash ring, how many keys move when nodes join, virtual nodes and hot keys.",
          "steps": 5,
          "runnable": false
        },
        {
          "id": "SY8",
          "title": "Load balancing and horizontal scaling",
          "section": "Scaling",
          "summary": "Round robin vs least connections, health checks, autoscaling and why servers must be stateless.",
          "steps": 5,
          "runnable": false
        },
        {
          "id": "SY9",
          "title": "Caching layers and strategies",
          "section": "Scaling",
          "summary": "Browser, CDN and Redis caches, cache-aside, invalidation races and the maths of hit ratios.",
          "steps": 5,
          "runnable": false
        },
        {
          "id": "SY10",
          "title": "Message queues, delivery guarantees and idempotency",
          "section": "Distributed systems",
          "summary": "Async decoupling, at-least-once duplicates, idempotent consumers, partitions and dead letters.",
          "steps": 5,
          "runnable": false
        },
        {
          "id": "SY11",
          "title": "Distributed transactions: outbox, 2PC and sagas",
          "section": "Distributed systems",
          "summary": "Why dual writes break, the transactional outbox, blocking two-phase commit, and compensating sagas.",
          "steps": 5,
          "runnable": false
        },
        {
          "id": "SY12",
          "title": "Consensus with Raft",
          "section": "Distributed systems",
          "summary": "Leader election by majority vote, log replication, commit rules, failover and partitions.",
          "steps": 6,
          "runnable": false
        },
        {
          "id": "SY13",
          "title": "Observability: logs, metrics, traces and SLOs",
          "section": "Reliability",
          "summary": "Distributed traces, percentiles vs averages, error budgets, and alerting on what users feel.",
          "steps": 5,
          "runnable": false
        },
        {
          "id": "SY14",
          "title": "Case study: designing a URL shortener",
          "section": "Case study",
          "summary": "Estimates, API, id generation, caching, partitioning and async analytics in one design.",
          "steps": 6,
          "runnable": false
        }
      ]
    },
    {
      "id": "ai",
      "title": "Machine Learning & AI",
      "tagline": "From linear regression and decision trees to transformers, LLM training, agents and 2026 frontier models.",
      "accent": "#E879F9",
      "monogram": "AI",
      "lessons": [
        {
          "id": "AI1",
          "title": "What machine learning is: fitting, underfitting and overfitting",
          "section": "Foundations",
          "summary": "Learning from examples, the three kinds of learning, bias vs variance and held-out validation.",
          "steps": 6,
          "runnable": false
        },
        {
          "id": "AI2",
          "title": "Linear regression and gradient descent",
          "section": "Classic machine learning",
          "summary": "Squared error, gradients, learning rates and convergence, animated on real numbers.",
          "steps": 5,
          "runnable": false
        },
        {
          "id": "AI3",
          "title": "Logistic regression, precision and recall",
          "section": "Classic machine learning",
          "summary": "Sigmoid probabilities, decision boundaries, confusion matrices and choosing a threshold.",
          "steps": 5,
          "runnable": false
        },
        {
          "id": "AI4",
          "title": "k-nearest neighbours, SVMs, kernels and naive Bayes",
          "section": "Classic machine learning",
          "summary": "Voting neighbours, maximum-margin boundaries, the kernel trick and probabilistic text classification.",
          "steps": 5,
          "runnable": false
        },
        {
          "id": "AI5",
          "title": "Decision trees, random forests and gradient boosting",
          "section": "Classic machine learning",
          "summary": "Splitting by impurity, bagging many trees, and boosting that fixes residual errors.",
          "steps": 5,
          "runnable": false
        },
        {
          "id": "AI6",
          "title": "Unsupervised learning: k-means clustering and PCA",
          "section": "Classic machine learning",
          "summary": "Assign-and-move clustering animated to convergence, and projecting data onto principal components.",
          "steps": 6,
          "runnable": false
        },
        {
          "id": "AI7",
          "title": "Neural networks: neurons, activations and layers",
          "section": "Neural networks",
          "summary": "Weighted sums, non-linear activations, forward passes, softmax and why XOR needs a hidden layer.",
          "steps": 6,
          "runnable": false
        },
        {
          "id": "AI8",
          "title": "Backpropagation and optimisers",
          "section": "Neural networks",
          "summary": "The chain rule on a real tiny network, weight updates, SGD vs momentum vs Adam, and training fixes.",
          "steps": 5,
          "runnable": false
        },
        {
          "id": "AI9",
          "title": "Convolutional neural networks",
          "section": "Neural networks",
          "summary": "Sliding filters computed live, feature maps, weight sharing, pooling and the CNN milestones.",
          "steps": 6,
          "runnable": false
        },
        {
          "id": "AI10",
          "title": "Recurrent networks, LSTMs and the road to attention",
          "section": "Neural networks",
          "summary": "Hidden state over time, vanishing gradients, gated memory, seq2seq and why attention won.",
          "steps": 5,
          "runnable": false
        },
        {
          "id": "AI11",
          "title": "Tokens and embeddings",
          "section": "Language models",
          "summary": "Tokenization with byte-pair encoding, embedding lookup, meaning as geometry and cosine similarity.",
          "steps": 5,
          "runnable": false
        },
        {
          "id": "AI12",
          "title": "Attention and the transformer",
          "section": "Language models",
          "summary": "Self-attention with live numbers, queries/keys/values, multi-head attention, the block, and causal masking.",
          "steps": 5,
          "runnable": false
        },
        {
          "id": "AI13",
          "title": "How large language models are trained",
          "section": "Language models",
          "summary": "Next-token pre-training, scaling laws, instruction tuning, RLHF/DPO and RL for reasoning.",
          "steps": 7,
          "runnable": false
        },
        {
          "id": "AI14",
          "title": "LLM inference: sampling, KV cache and speed",
          "section": "Language models",
          "summary": "The generation loop, temperature and top-p, KV-cache memory maths and why decoding is bandwidth-bound.",
          "steps": 6,
          "runnable": false
        },
        {
          "id": "AI15",
          "title": "Reinforcement learning: rewards, values and policies",
          "section": "Learning from rewards",
          "summary": "The agent loop, value iteration computed live on a grid world, exploration, and RL milestones.",
          "steps": 6,
          "runnable": false
        },
        {
          "id": "AI16",
          "title": "Generative models: GANs, VAEs and diffusion",
          "section": "Generative AI",
          "summary": "Adversarial training, latent spaces, and diffusion animated from noise to image.",
          "steps": 8,
          "runnable": false
        },
        {
          "id": "AI17",
          "title": "RAG, tool calling and agents",
          "section": "Applied AI",
          "summary": "Grounding answers in your data, letting models call functions, agent loops and prompt-injection guardrails.",
          "steps": 6,
          "runnable": false
        },
        {
          "id": "AI18",
          "title": "Frontier models: mixture of experts, reasoning and the 2026 landscape",
          "section": "Frontier models",
          "summary": "Sparse experts, test-time reasoning, multimodal agents and a dated timeline up to November 2026.",
          "steps": 7,
          "runnable": false
        }
      ]
    },
    {
      "id": "np",
      "title": "Interfaces & Protocols",
      "tagline": "Ethernet, IP, TCP, QUIC, Wi-Fi, Bluetooth, USB, PCIe, HDMI/DP, CAN, IoT and 5G/6G, up to what is in development.",
      "accent": "#38BDF8",
      "monogram": "IO",
      "lessons": [
        {
          "id": "IO1",
          "title": "Network layers and encapsulation",
          "section": "Networking",
          "summary": "OSI vs TCP/IP, and how HTTP gets wrapped in TLS, TCP, IP and Ethernet headers.",
          "steps": 6,
          "runnable": false
        },
        {
          "id": "IO2",
          "title": "Ethernet: frames, switches and speeds",
          "section": "Networking",
          "summary": "Frame layout, MAC learning in switches, and Ethernet from 10 Mb/s to 1.6 Tb/s.",
          "steps": 5,
          "runnable": false
        },
        {
          "id": "IO3",
          "title": "IP, ARP and routing",
          "section": "Networking",
          "summary": "Subnets, ARP, gateways, NAT, longest-prefix routing and IPv6 addresses.",
          "steps": 5,
          "runnable": false
        },
        {
          "id": "IO4",
          "title": "TCP: handshake, ACKs, loss and congestion",
          "section": "Networking",
          "summary": "A sequence-diagram walk through connecting, sending, losing, retransmitting and closing.",
          "steps": 5,
          "runnable": false
        },
        {
          "id": "IO5",
          "title": "UDP, QUIC and HTTP/3",
          "section": "Networking",
          "summary": "Datagrams, head-of-line blocking, QUIC streams, 1-RTT handshakes and connection migration.",
          "steps": 5,
          "runnable": false
        },
        {
          "id": "IO6",
          "title": "Wi-Fi: sharing the air",
          "section": "Wireless",
          "summary": "CSMA/CA, collisions, OFDMA, and Wi-Fi generations through Wi-Fi 7 and 8.",
          "steps": 5,
          "runnable": false
        },
        {
          "id": "IO7",
          "title": "USB: enumeration, transfer types and speeds",
          "section": "Peripheral buses",
          "summary": "How a device is discovered and driven, the four transfer types, and USB 1.0 to USB4 v2.",
          "steps": 5,
          "runnable": false
        },
        {
          "id": "IO8",
          "title": "USB-C, Power Delivery, USB4 and Thunderbolt",
          "section": "Peripheral buses",
          "summary": "Connector vs protocol, PD negotiation up to 240 W, USB4 tunnelling and Thunderbolt 5.",
          "steps": 5,
          "runnable": false
        },
        {
          "id": "IO9",
          "title": "Bluetooth: Classic, LE, GATT and LE Audio",
          "section": "Wireless",
          "summary": "Advertising, channel hopping, GATT services, Auracast and versions through 6.x.",
          "steps": 6,
          "runnable": false
        },
        {
          "id": "IO10",
          "title": "PCI Express, NVMe and CXL",
          "section": "Peripheral buses",
          "summary": "Lanes, packets, generations 1.0 to 8.0, NVMe queues, and CXL/NVLink/UCIe.",
          "steps": 6,
          "runnable": false
        },
        {
          "id": "IO11",
          "title": "Display links: HDMI and DisplayPort",
          "section": "Peripheral buses",
          "summary": "Pixel bandwidth maths, TMDS vs FRL, DisplayPort MST, DSC, and HDMI 2.2 / DP 2.1.",
          "steps": 5,
          "runnable": false
        },
        {
          "id": "IO12",
          "title": "Embedded buses: UART, I2C, SPI and CAN",
          "section": "Embedded and IoT",
          "summary": "A UART waveform, I2C addressing, SPI chip selects, CAN arbitration and CAN XL.",
          "steps": 5,
          "runnable": false
        },
        {
          "id": "IO13",
          "title": "Wireless IoT and cellular: Thread, Matter, NFC, LoRa, 5G/6G",
          "section": "Embedded and IoT",
          "summary": "A range-vs-rate map of every wireless link, from NFC to 6G.",
          "steps": 5,
          "runnable": false
        },
        {
          "id": "IO14",
          "title": "The interface atlas: latest and next",
          "section": "Overview",
          "summary": "Every interface in one table: the newest released version and what is in development.",
          "steps": 5,
          "runnable": false
        }
      ]
    },
    {
      "id": "rm",
      "title": "Resource Management",
      "tagline": "Pools, rate limits, retries, caches and queues in production systems.",
      "accent": "#FB923C",
      "monogram": "RM",
      "lessons": [
        {
          "id": "RM1",
          "title": "Connection pool exhaustion",
          "section": "Pools and limits",
          "summary": "How a single missing Dispose drains a pool and times out a healthy service.",
          "steps": 5,
          "runnable": false
        },
        {
          "id": "RM2",
          "title": "Rate limiting with a token bucket",
          "section": "Pools and limits",
          "summary": "Bursts versus sustained rate, simulated tick by tick.",
          "steps": 10,
          "runnable": false
        },
        {
          "id": "RM3",
          "title": "Retry storms, backoff and jitter",
          "section": "Resilience",
          "summary": "How well-meant retries can keep a service down, and the backoff strategy that prevents it.",
          "steps": 4,
          "runnable": false
        },
        {
          "id": "RM4",
          "title": "Cache stampede (thundering herd)",
          "section": "Resilience",
          "summary": "What happens when a hot cache key expires under load, and single-flight fixes.",
          "steps": 4,
          "runnable": false
        },
        {
          "id": "RM5",
          "title": "Circuit breaker",
          "section": "Resilience",
          "summary": "Closed, open, half-open: failing fast to protect both caller and dependency.",
          "steps": 4,
          "runnable": false
        },
        {
          "id": "RM6",
          "title": "Leaking handles and sockets",
          "section": "Pools and limits",
          "summary": "IDisposable, finalisers, TIME_WAIT and the classic HttpClient mistake.",
          "steps": 4,
          "runnable": false
        },
        {
          "id": "RM7",
          "title": "Queueing: why 90% utilisation hurts",
          "section": "Pools and limits",
          "summary": "The 1 / (1 - p) curve behind sudden latency cliffs, and Little's law.",
          "steps": 4,
          "runnable": false
        }
      ]
    }
  ]
};
