using grimoire.Abstractions;
using grimoire.Scenarios.DataStructures;
using grimoire.Scenarios.Deadlocks;
using grimoire.Scenarios.Synchronization;
using grimoire.Model;

namespace grimoire.Stories;

public static class Curriculum
{
    private static readonly Lazy<IReadOnlyList<Track>> LazyTracks = new(Build);

    public static IReadOnlyList<Track> Tracks => LazyTracks.Value;

    private static IReadOnlyList<Track> Build() =>
    [
        new Track("dt", "Data Representation", "Bits, hex, integers, floats, text encodings, struct layout and what a memory address means.", "#A3E635", "DR", DataRepresentationLessons.All().ToList()),
        new Track("ar", "Processors & Instruction Sets", "x86, ARM, RISC-V and GPU ISAs: decoding, pipelines, out-of-order, SIMD and branch prediction.", "#F472B6", "IS", ArchitectureLessons.All().ToList()),
        new Track("mm", "Memory Management", "Stack, heap, virtual memory, paging, allocators, garbage collection and caches.", "#C084FC", "MM", MemoryLessons.All().ToList()),
        new Track("os", "Operating Systems", "Processes, system calls, scheduling and classic deadlock problems.", "#FBBF24", "OS", OperatingSystemLessons.All().ToList()),
        new Track("cc", "Concurrency & Locks", "Deadlocks, races, memory visibility and the primitives that tame them.", "#F87171", "CC", Concurrency()),
        new Track("ds", "Data Structures", "How arrays, lists, hash tables, trees, heaps and filters really work in memory.", "#34D399", "DS", DataStructureLessons.All().ToList()),
        new Track("al", "Algorithms", "Searching, sorting, graphs and dynamic programming, step by step.", "#60A5FA", "AL", AlgorithmLessons.All().ToList()),
        new Track("gp", "GPU Computing", "SIMT warps, divergence, coalescing, transfers, occupancy and VRAM.", "#22D3EE", "GP", GpuLessons.All().ToList()),
        new Track("sw", "Software Design", "OOP, SOLID, design patterns, clean architecture, DDD, CQRS and robust, testable code.", "#818CF8", "SW", SoftwareDesignLessons.All().ToList()),
        new Track("sy", "System Design", "REST, ACID, isolation, CAP, replication, sharding, caching, queues, sagas, Raft and observability.", "#2DD4BF", "SY", SystemDesignLessons.All().ToList()),
        new Track("ai", "Machine Learning & AI", "From linear regression and decision trees to transformers, LLM training, agents and 2026 frontier models.", "#E879F9", "AI", AiLessons.All().ToList()),
        new Track("np", "Interfaces & Protocols", "Ethernet, IP, TCP, QUIC, Wi-Fi, Bluetooth, USB, PCIe, HDMI/DP, CAN, IoT and 5G/6G, up to what is in development.", "#38BDF8", "IO", InterfaceLessons.All().ToList()),
        new Track("rm", "Resource Management", "Pools, rate limits, retries, caches and queues in production systems.", "#FB923C", "RM", ResourceLessons.All().ToList())
    ];

    private static List<Lesson> Concurrency()
    {
        IScenario[] scenarios =
        [
            new LockOrderingDeadlockScenario(),
            new LockOrderingFixScenario(),
            new TryEnterBackoffScenario(),
            new SyncOverAsyncDeadlockScenario(),
            new ThreadPoolStarvationScenario(),
            new ReentrancySelfDeadlockScenario(),
            new MissedPulseDeadlockScenario(),
            new ReaderWriterUpgradeDeadlockScenario(),
            new LostUpdateScenario(),
            new PrimitiveComparisonScenario(),
            new CoordinationPrimitivesScenario(),
            new MemoryVisibilityScenario(),
            new AsyncMutexScenario(),
            new UnsafeVsSafeResourceScenario(),
            new DictionaryCorruptionScenario(),
            new CompoundOperationScenario(),
            new ProducerConsumerScenario(),
            new SnapshotAndCopyOnWriteScenario()
        ];

        var stories = DeadlockStories.All()
            .Concat(SynchronizationStories.All())
            .Concat(ConcurrentCollectionStories.All())
            .ToDictionary(s => s.Id);

        return scenarios
            .Select(s => new Lesson(
                s.Id,
                s.Title,
                s.Description,
                s.Category switch
                {
                    ScenarioCategory.Deadlock => "Deadlocks and how to avoid them",
                    ScenarioCategory.Synchronization => "Synchronization primitives",
                    _ => "Thread-safe collections"
                },
                stories[s.Id],
                s))
            .ToList();
    }
}
