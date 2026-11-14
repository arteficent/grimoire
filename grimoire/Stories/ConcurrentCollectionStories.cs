using grimoire.Model;
using static grimoire.Model.ThreadPhase;
using static grimoire.Model.Tone;
using RK = grimoire.Model.ResourceKind;

namespace grimoire.Stories;

internal static class ConcurrentCollectionStories
{
    public static IEnumerable<Story> All()
    {
        yield return UnsafeList();
        yield return DictionaryCorruption();
        yield return CompoundOperation();
        yield return ProducerConsumer();
        yield return SnapshotCopyOnWrite();
    }

    // ------------------------------------------------------------------------------------------
    private static Story UnsafeList() => new StoryBuilder("C1",
            "A corrupted List<T> usually loses data silently instead of throwing. Protect the whole compound operation, or use the System.Collections.Concurrent types.")
        .Setup(s =>
        {
            s.AddCores(2);
            s.AddThread("t1", "writer 1");
            s.AddThread("t2", "writer 2");
            s.AddResource("L", "List<int>", RK.Collection, 0.75, 0.35, value: "_size = 3, capacity 4");
            s.Items("L", "10", "11", "12", "_");
            s.Ram("_size", "3").Ram("_items[3]", "0");
        })
        .Code("""
            // List<T>.Add, simplified
            public void Add(T item)
            {
                T[] array = _items;
                int size  = _size;              // 1. read the count
                if ((uint)size < (uint)array.Length)
                {
                    _size = size + 1;           // 2. bump the count
                    array[size] = item;         // 3. write the slot
                }
                else AddWithResize(item);       // 4. new array, copy, swap reference
            }
            """)
        .Step("Two writers call Add at once",
            "The real demo uses 8 threads x 5,000 adds on List, Dictionary and Queue. Here, two threads add one item each to a list holding 3 items with capacity 4.",
            "Both threads run on separate cores. List<T> has no internal synchronisation: _size and _items are plain fields.",
            [2],
            s => s.Run("t1", 0).Run("t2", 1).Go("t1", "L").Go("t2", "L")
                  .Note("t1", "Add(100)").Note("t2", "Add(200)"))
        .Step("Both read _size = 3",
            "Each thread reads the current count into a local variable.",
            "Both cores fetch the line holding _size in Shared state. Each has size = 3 in a register.",
            [5],
            s => s.Reg(0, "size = 3").Reg(1, "size = 3")
                  .Cache(0, "_size", "3", 'S').Cache(1, "_size", "3", 'S')
                  .Move("ram:_size", "core:0", "load 3").Move("ram:_size", "core:1", "load 3"))
        .Step("Both write slot 3",
            "Writer 1 writes 100 into slot 3. Writer 2 writes 200 into the same slot. Both set _size = 4.",
            "Two stores to array[3] are serialised by coherence: the last one wins. The two _size = 4 stores are also identical, so nothing looks inconsistent afterwards.",
            [8, 9],
            s => s.Items("L", "10", "11", "12", "200")
                  .Set("L", "_size = 4 (expected 5)", Bad)
                  .Cache(0, "_size", "4", 'I').Cache(1, "_size", "4", 'M')
                  .Ram("_items[3]", "200 (100 overwritten)", Bad)
                  .Note("t1", "my 100 vanished")
                  .Say("SILENT DATA LOSS: 100 was overwritten, with no exception", Bad))
        .Step("A resize race is worse",
            "The list is now full. Both threads take the resize path. Each allocates its own 8-slot array, copies the items, and writes the reference into _items.",
            "Two separate arrays now exist on the heap. Whichever store to _items happens last wins, and everything the other core wrote went into an array that nothing references any more. _size can also end up past the array's length, which causes IndexOutOfRangeException or default (0/null) slots.",
            [11],
            s => s.Items("L", "10", "11", "12", "200", "0", "300")
                  .Set("L", "_size = 6, slot 4 = default", Bad)
                  .Note("t1", "wrote into an orphaned array")
                  .Phase("t2", Faulted).Note("t2", "IndexOutOfRangeException"))
        .Code("""
            // Thread-safe resource
            lock (_gate) { _list.Add(item); }   // whole Add is one critical section
            _dictionary.TryAdd(key, value);     // ConcurrentDictionary
            _queue.Enqueue(item);               // ConcurrentQueue
            """)
        .Step("Fix: guard the whole operation",
            "The read-check-write-increment sequence becomes one critical section. The concurrent collections do this internally, more efficiently.",
            "Monitor.Exit's release store makes sure the next owner sees both the new slot and the new _size. ConcurrentQueue uses Interlocked on segment indices instead of a lock.",
            [2, 3, 4],
            s => s.Run("t2", 1).Phase("t2", Running).Go("t1", "L").Go("t2", "L")
                  .Items("L", "10", "11", "12", "100", "200")
                  .Set("L", "_size = 5", Good)
                  .Ram("_items[3]", "100", Good)
                  .Say("Thread-safe resource: 40,000 of 40,000 items", Good))
        .Build();

    // ------------------------------------------------------------------------------------------
    private static Story DictionaryCorruption() => new StoryBuilder("C2",
            "Hash table corruption has no upper bound on damage. The instance can become permanently unusable. Never write to a Dictionary from more than one thread without a lock.")
        .Setup(s =>
        {
            s.AddCores(2);
            s.AddThread("w1", "writer 1");
            s.AddThread("w2", "writer 2");
            s.AddResource("D", "Dictionary<int,int>", RK.Collection, 0.6, 0.0, value: "4 buckets, count = 6");
            s.Items("D", "b0: 4", "b1: 9->1", "b2: -", "b3: 7->3->11");
            s.Ram("_buckets", "int[4]").Ram("_entries", "Entry[6]");
        })
        .Code("""
            // Dictionary<TKey,TValue> internals (simplified)
            int[]   _buckets;    // bucket -> index of first entry
            Entry[] _entries;    // { key, value, next }, chained via 'next'

            // writer 1 and writer 2, no lock:
            plain[key] = value;  // may trigger Resize(): new arrays, re-hash every entry
            """)
        .Step("How a Dictionary is laid out",
            "Keys hash to buckets. Each bucket points to a chain of entries linked by a 'next' index.",
            "These are two parallel arrays. A lookup does hash, then _buckets[h], then follows entry.next until the key matches or it reaches -1.",
            [2, 3],
            s => s.Run("w1", 0).Run("w2", 1))
        .Step("Both insert, and one triggers a resize",
            "Writer 1 runs out of room and starts Resize(): it allocates 8 buckets and re-hashes every entry. Writer 2 is still inserting into the old arrays.",
            "Resize writes thousands of 'next' indices into the new arrays. None of these stores is ordered relative to writer 2's stores, because there are no barriers.",
            [6],
            s => s.Go("w1", "D").Go("w2", "D")
                  .Note("w1", "Resize(): 8 buckets, re-hash")
                  .Note("w2", "insert into OLD arrays")
                  .Set("D", "resizing...", Warn)
                  .Move("core:0", "ram:_buckets", "new int[8]")
                  .Move("core:1", "ram:_entries", "entry.next = 9"))
        .Step("Torn state: a cycle in a chain",
            "One entry's 'next' now points back into its own chain. The structure is no longer a valid hash table.",
            "Core 0 and Core 1 each made writes that are valid on their own, but mixed together they produce a graph that no single-threaded run could create. There is nothing to repair it, because the corruption is just data.",
            [6],
            s => s.Items("D", "b0: 4", "b1: 9->1->9->...", "b2: 6", "b3: 7->3", "b5: 11")
                  .Set("D", "count = 7, chain cycle", Bad)
                  .Ram("_entries", "Entry[12] (torn)", Bad))
        .Step("What you see: a hang or an exception",
            "On .NET Framework, a lookup could follow the cycle forever and pin a core at 100%. Modern .NET counts the steps and throws InvalidOperationException ('concurrent operations not supported'). Entries can also simply go missing.",
            "A thread stuck following the cycle never makes a system call and never sleeps. It is a pure CPU livelock, so watchdogs that only look for blocked threads miss it.",
            [6],
            s => s.Phase("w1", Spinning).Note("w1", "TryGetValue walks 9 -> 1 -> 9 -> ...")
                  .Phase("w2", Faulted).Note("w2", "InvalidOperationException")
                  .Say("CORRUPTED: an infinite loop, an exception, or lost entries", Bad))
        .Setup(s =>
        {
            s.AddResource("S0", "stripe lock 0", RK.Lock, 0.25, 1.0, value: "free");
            s.AddResource("S1", "stripe lock 1", RK.Lock, 0.95, 1.0, value: "free");
        })
        .Code("""
            var map = new ConcurrentDictionary<int, int>();
            map[key] = value;    // takes only the stripe lock for that bucket range
            map.TryGetValue(key, out var v);   // lock-free read
            """)
        .Step("Fix: ConcurrentDictionary uses striped locks",
            "Writes take only the lock for their bucket's stripe, so writers to different stripes run in parallel. Reads take no lock at all.",
            "Buckets are published with volatile writes, so a reader always sees a complete node. Resizing takes all stripe locks, which is rare and amortised.",
            [1, 2, 3],
            s =>
            {
                s.R("D").Label = "ConcurrentDictionary";
                s.Items("D", "b0: 4", "b1: 9->1", "b2: 6", "b3: 7->3", "b5: 11")
                 .Set("D", "count = 7", Good).Ram("_entries", "nodes (immutable)")
                 .Phase("w1", Running).Phase("w2", Running).Run("w1", 0).Run("w2", 1)
                 .Acquire("w1", "S0").Acquire("w2", "S1")
                 .Set("S0", "owner = writer 1").Set("S1", "owner = writer 2");
            })
        .Step("Correct under contention",
            "120,000 writes give 120,000 entries and 0 exceptions.",
            "Lock striping trades a little memory (one lock per stripe) for parallel writes.",
            [2],
            s => s.Release("w1", "S0").Release("w2", "S1").Set("S0", "free").Set("S1", "free")
                  .Phase("w1", Done).Phase("w2", Done)
                  .Say("ConcurrentDictionary: 120,000 of 120,000 entries, 0 exceptions", Good))
        .Build();

    // ------------------------------------------------------------------------------------------
    private static Story CompoundOperation() => new StoryBuilder("C3",
            "Atomicity is a property of an operation, not of a type. Use the composite APIs (TryAdd, GetOrAdd, AddOrUpdate), and wrap expensive factories in Lazy<T>.")
        .Setup(s =>
        {
            s.AddCores(3);
            s.AddThread("t1", "thread 1");
            s.AddThread("t2", "thread 2");
            s.AddThread("t3", "thread 3");
            s.AddResource("M", "ConcurrentDictionary", RK.Collection, 0.5, 0.0, value: "empty");
        })
        .Code("""
            if (!map.ContainsKey("key"))      // atomic on its own
            {
                Thread.Sleep(1);              // ...the window
                map.TryAdd("key", i);         // atomic on its own
            }
            """)
        .Step("Everyone checks first",
            "Three threads ask ContainsKey(\"key\"). All of them get false.",
            "Each call is a lock-free read: a volatile load of the bucket and a walk of its nodes. Every answer is correct at the moment it is given.",
            [1],
            s => s.Run("t1", 0).Run("t2", 1).Run("t3", 2)
                  .Go("t1", "M").Go("t2", "M").Go("t3", "M")
                  .Note("t1", "missing!").Note("t2", "missing!").Note("t3", "missing!"))
        .Step("Thread 1's TryAdd wins",
            "Thread 1 inserts the key.",
            "It takes the stripe lock, links a new node, and publishes it with a release store.",
            [4],
            s => s.Items("M", "key = 1").Set("M", null).Note("t1", "TryAdd = true"))
        .Step("The others fail on an 'impossible' case",
            "Threads 2 and 3 checked and saw the key was missing, yet TryAdd fails for them. Code that assumes it must succeed then breaks.",
            "Two atomic operations in a row are not one atomic operation. Any number of other operations can happen between them.",
            [4],
            s => s.Note("t2", "TryAdd = false, but it was missing!")
                  .Note("t3", "TryAdd = false, but it was missing!")
                  .Say("Check-then-act race: two atomic operations are not one atomic operation", Bad))
        .Setup(s => s.AddResource("F", "ExpensiveFactory()", RK.Task, 0.5, 1.0, value: "runs: 0"))
        .Code("""
            var value = map.GetOrAdd("connection", _ => ExpensiveFactory());

            // fix: let Lazy<T> guarantee a single execution
            var lazy = lazyMap.GetOrAdd("connection",
                _ => new Lazy<string>(ExpensiveFactory, LazyThreadSafetyMode.ExecutionAndPublication));
            var value = lazy.Value;
            """)
        .Step("GetOrAdd: the factory runs on every thread",
            "Three threads call GetOrAdd(\"connection\", factory) on an empty map. All three run the factory.",
            "GetOrAdd deliberately calls your factory outside its internal lock, so that user code never runs while a lock is held. Then it tries an atomic insert. The losers' results are thrown away, which wastes work or leaks connections, sockets and file handles.",
            [1],
            s => s.Items("M").Set("M", "empty", Neutral)
                  .Go("t1", "F").Go("t2", "F").Go("t3", "F")
                  .Set("F", "runs: 3", Bad))
        .Step("Only one result is kept",
            "Every caller gets the same stored value, but two factory runs were wasted.",
            "Losing nodes become garbage and are collected, but any side effects they had cannot be undone.",
            [1],
            s => s.Items("M", "connection = #1")
                  .Note("t2", "my #2 was discarded").Note("t3", "my #3 was discarded")
                  .Go("t1", "M").Go("t2", "M").Go("t3", "M"))
        .Step("Fix: wrap the factory in Lazy<T>",
            "GetOrAdd now stores a cheap Lazy wrapper. Even if several Lazy objects are created, only the stored one is ever evaluated, and Lazy runs its factory exactly once.",
            "In ExecutionAndPublication mode, Lazy uses a lock and a double-checked volatile read: the first thread runs the factory and the others block until the value is published.",
            [4, 5, 6],
            s => s.Items("M", "connection = Lazy<string>").Set("F", "runs: 1 (in progress)", Info)
                  .Acquire("t1", "F").WaitOn("t2", "F").WaitOn("t3", "F")
                  .Unschedule("t2").Unschedule("t3"))
        .Step("Exactly one execution",
            "All three threads get the same instance, and the factory ran once.",
            "Afterwards, lazy.Value is a single volatile read of an already published reference.",
            [6],
            s => s.Release("t1", "F").StopWaiting("t2", "F").StopWaiting("t3", "F")
                  .Phase("t1", Done).Phase("t2", Done).Phase("t3", Done)
                  .Go("t1", null).Go("t2", null).Go("t3", null)
                  .Set("F", "runs: 1", Good)
                  .Say("Factory ran once: Lazy<T> plus GetOrAdd", Good))
        .Build();

    // ------------------------------------------------------------------------------------------
    private static Story ProducerConsumer() => new StoryBuilder("C4",
            "A bounded BlockingCollection gives back-pressure without polling. A missing CompleteAdding is a guaranteed hang. For async code, prefer Channel<T>.")
        .Setup(s =>
        {
            s.AddCores(3);
            s.AddThread("p1", "producer 1");
            s.AddThread("p2", "producer 2");
            s.AddThread("c", "consumer");
            s.AddResource("Q", "Queue<int>", RK.Queue, 0.5, 0.35, value: "_head, _tail, _size");
        })
        .Code("""
            // 1. Queue<T> with no lock
            queue.Enqueue(item);                         // producers
            if (queue.TryDequeue(out var x)) Use(x);     // consumer

            // 2. ConcurrentQueue<T>: correct, but the consumer must poll
            while (!queue.TryDequeue(out x)) spin.SpinOnce();
            """)
        .Step("Queue<T>: same race as List<T>",
            "Two producers and one consumer use a plain Queue<T>.",
            "Enqueue does: read _tail, write the slot, advance _tail, increment _size. Dequeue does the same at _head. Every one of those fields is raced on.",
            [2, 3],
            s => s.Run("p1", 0).Run("p2", 1).Run("c", 2)
                  .Go("p1", "Q").Go("p2", "Q").Go("c", "Q")
                  .Items("Q", "1", "2", "3"))
        .Step("Corruption",
            "Items are lost or duplicated, default values appear, and eventually an exception is thrown from inside the queue.",
            "_size and _tail get out of step. The consumer reads a slot that a producer has not written yet and gets 0.",
            [2, 3],
            s => s.Items("Q", "1", "0", "3", "3")
                  .Set("Q", "_size = 2, but 4 slots look full", Bad)
                  .Phase("c", Faulted).Note("c", "InvalidOperationException")
                  .Say("Queue<T>: consumed far fewer items than produced, plus exceptions", Bad))
        .Step("ConcurrentQueue<T>: correct but busy",
            "Every item arrives exactly once. When the queue is empty, though, the consumer has to keep asking.",
            "TryDequeue is lock-free (an Interlocked increment on segment indices). An empty queue means calling SpinOnce in a loop. The core is busy doing nothing, and that burns power and steals time from the producers.",
            [6],
            s =>
            {
                s.R("Q").Label = "ConcurrentQueue<int>";
                s.Items("Q").Set("Q", "empty, TryDequeue misses: 1.4 M", Warn)
                 .Phase("c", Spinning).Run("c", 2).Go("c", "Q")
                 .Reg(2, "SpinOnce(); TryDequeue()")
                 .Note("c", "anything yet? anything yet?");
            })
        .Code("""
            // 3. BlockingCollection, but the producer forgets CompleteAdding
            foreach (var x in bc.GetConsumingEnumerable()) Use(x);

            // 4. Done right: bounded capacity + CompleteAdding
            using var bc = new BlockingCollection<int>(boundedCapacity: 4);
            producer: foreach (var i in items) bc.Add(i);    // blocks when full
                      bc.CompleteAdding();
            consumer: foreach (var x in bc.GetConsumingEnumerable()) Use(x);
            """)
        .Step("BlockingCollection: the consumer sleeps properly",
            "GetConsumingEnumerable puts the consumer to sleep when the collection is empty. No polling.",
            "Inside, a SemaphoreSlim counts the items. Take waits on it: it spins briefly and then sleeps on a kernel event, so the core is free.",
            [2],
            s =>
            {
                s.R("Q").Label = "BlockingCollection<int>";
                s.Set("Q", "empty", Neutral).Reg(2, null)
                 .Phase("p1", Done).Phase("p2", Done).Go("p1", null).Go("p2", null)
                 .WaitOn("c", "Q").Unschedule("c");
            })
        .Step("...but nobody says 'no more items'",
            "The producers finished without calling CompleteAdding(). The consumer has used up every item and keeps waiting for the next one.",
            "The consumer sleeps forever. The enumeration ends only when IsAddingCompleted is set and the collection is empty.",
            [2],
            s => s.Set("Q", "empty, adding not completed", Bad)
                  .Deadlock("c")
                  .Say("HANG: the consumer drained everything and is still blocked", Bad))
        .Step("Done right: bounded capacity gives back-pressure",
            "With boundedCapacity: 4, producers that get ahead block in Add until the consumer catches up.",
            "Back-pressure keeps memory use bounded. A full collection makes producers sleep on a second semaphore that counts free slots.",
            [5, 6],
            s => s.StopWaiting("c", "Q").ClearDeadlock()
                  .Phase("p1", Running).Phase("p2", Running).Run("p1", 0).Run("p2", 1).Run("c", 2)
                  .Items("Q", "41", "42", "43", "44").Set("Q", "full (4/4)", Warn)
                  .WaitOn("p1", "Q").WaitOn("p2", "Q").Unschedule("p1").Unschedule("p2")
                  .Go("c", "Q").Note("p1", "Add() blocks: queue is full"))
        .Step("CompleteAdding ends the stream cleanly",
            "The producers finish and call CompleteAdding(). The consumer drains the remaining items and its foreach ends.",
            "CompleteAdding sets a flag and wakes all sleeping consumers, so they can see that the stream has ended.",
            [7, 8],
            s => s.StopWaiting("p1", "Q").StopWaiting("p2", "Q")
                  .Phase("p1", Done).Phase("p2", Done).Phase("c", Done)
                  .Items("Q").Set("Q", "completed, empty", Good)
                  .Say("2,000 items consumed with a bounded buffer and no polling", Good))
        .Build();

    // ------------------------------------------------------------------------------------------
    private static Story SnapshotCopyOnWrite() => new StoryBuilder("C5",
            "'Thread-safe' does not say which consistency model you get. Copy-on-write is ideal for read-mostly data: readers take no lock and always see a consistent version.")
        .Setup(s =>
        {
            s.AddCores(2);
            s.AddThread("r", "reader (foreach)");
            s.AddThread("w", "writer");
            s.AddResource("L", "List<int>", RK.Collection, 0.55, 0.35, value: "_version = 17");
            s.Items("L", "1", "2", "3", "4");
        })
        .Code("""
            // 1. List<T> mutated during foreach
            foreach (var x in list) { ... }    // enumerator remembers _version
            list.Add(5);                       // another thread: _version++

            // 2. ConcurrentDictionary: a moving view
            var a = dict.Count();  var b = dict.Count();   // a != b
            """)
        .Step("The enumerator remembers the version",
            "foreach over a List<T> creates an enumerator that remembers _version, a counter that is incremented by every change to the list.",
            "That is one integer copied into the enumerator struct. Every MoveNext compares it with the list's current _version.",
            [2],
            s => s.Run("r", 0).Go("r", "L").Note("r", "enumerator.version = 17"))
        .Step("A writer modifies the list",
            "Another thread adds an item, and _version becomes 18.",
            "Note that in the demo the writer holds its own lock, but the reader does not take it. A lock only helps if every thread that touches the data uses it.",
            [3],
            s => s.Run("w", 1).Go("w", "L").Items("L", "1", "2", "3", "4", "5")
                  .Set("L", "_version = 18", Warn)
                  .Move("core:1", "core:0", "invalidate _version"))
        .Step("MoveNext throws",
            "\"Collection was modified; enumeration operation may not execute.\" That is if you are lucky. When the two threads are not synchronised, the check itself is a race and may miss the change.",
            "The version check is best-effort. Without a barrier, Core 0 may not have observed the new _version yet.",
            [2],
            s => s.Phase("r", Faulted).Note("r", "InvalidOperationException")
                  .Phase("w", Done).Go("w", null)
                  .Say("List<T>: enumerating while writing throws, or quietly reads a torn state", Bad))
        .Step("ConcurrentDictionary: a moving view",
            "Enumerating a ConcurrentDictionary never throws, but it is not a snapshot. Two Count() calls in a row return 1000 and 1042.",
            "The enumerator walks the buckets without any lock. Each read is safe, but items added behind or ahead of the cursor may or may not appear.",
            [6],
            s =>
            {
                s.R("L").Label = "ConcurrentDictionary";
                s.Phase("r", Running).Run("r", 0).Go("r", "L")
                 .Phase("w", Running).Run("w", 1).Go("w", "L")
                 .Items("L").Set("L", "Count(): 1000, then 1042", Warn)
                 .Note("r", "both values are 'correct'");
            })
        .Setup(s =>
        {
            s.AddResource("V1", "array v1", RK.Collection, 0.1, 1.0);
            s.AddResource("V2", "array v2", RK.Collection, 0.85, 1.0, value: "-");
            s.Items("V1", "1", "2", "3");
        })
        .Code("""
            // 3. Copy-on-write
            T[] _items = [];                                // the only shared field

            T[] Snapshot() => Volatile.Read(ref _items);    // readers: one load

            void Add(T item)
            {
                while (true)
                {
                    var current = Volatile.Read(ref _items);
                    T[] next = [.. current, item];          // private copy
                    if (Interlocked.CompareExchange(ref _items, next, current) == current) return;
                }
            }
            """)
        .Step("Copy-on-write: one shared reference",
            "The store holds a single reference to an immutable array. A reader grabs the current reference once and enumerates that array.",
            "Taking a snapshot is a single pointer-sized load. Every reader shares the line in S state, so reads scale perfectly.",
            [2, 4],
            s =>
            {
                s.R("L").Label = "_items";
                s.R("L").Kind = RK.Variable;
                s.Set("L", "-> array v1", Info).Items("L")
                 .Go("r", "V1").Go("w", null)
                 .Note("r", "enumerating v1 (immutable)")
                 .Cache(0, "_items", "&v1", 'S');
            })
        .Step("The writer builds a private copy",
            "The writer copies v1, appends its item, and gets v2. Nobody else can see v2 yet.",
            "These are allocations and copies in Core 1's own cache lines, with no sharing and no coherence traffic. Writes cost O(n), which is the price of copy-on-write.",
            [10, 11],
            s => s.Go("w", "V2").Items("V2", "1", "2", "3", "4").Set("V2", null)
                  .Note("w", "next = [.. current, 4]"))
        .Step("Publish with CompareExchange",
            "One atomic swap makes v2 the current version. If another writer published first, the swap fails and the loop retries with the newer array.",
            "lock cmpxchg on the _items line moves it to Core 1 in M state, and Core 0's copy is invalidated. The reader still holds its reference to v1, which is still complete and consistent.",
            [12],
            s => s.Set("L", "-> array v2", Good)
                  .Cache(1, "_items", "&v2", 'M').Invalidate(0, "_items")
                  .Move("core:1", "core:0", "invalidate _items")
                  .Note("r", "still reading v1: consistent"))
        .Step("Old versions are collected later",
            "When the last reader of v1 finishes, the GC reclaims it. The real demo found 0 inconsistent snapshots in 2,000 writes.",
            "Deciding when it is safe to free an old version is the hard part of this pattern in C++ (hazard pointers, RCU grace periods). In .NET the garbage collector does it for you. ImmutableArray and ImmutableList are ready-made versions of this pattern.",
            [4],
            s => s.Phase("r", Done).Phase("w", Done).Go("r", null).Go("w", null)
                  .Set("V1", "unreferenced, GC will collect", Neutral).Items("V1")
                  .Say("Copy-on-write: lock-free readers, always a consistent version", Good))
        .Build();
}
