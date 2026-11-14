using grimoire.Model;
using static grimoire.Model.Tone;

namespace grimoire.Stories;

internal static class DataStructureLessons
{
    public static IEnumerable<Lesson> All()
    {
        yield return ArrayVsList();
        yield return StackAndQueue();
        yield return HashTable();
        yield return BinarySearchTree();
        yield return BinaryHeap();
        yield return Trie();
        yield return LruCache();
        yield return BloomFilter();
        yield return BTree();
    }

    // ------------------------------------------------------------------------------------------
    private static Lesson ArrayVsList()
    {
        string[] addresses = ["0x7F20", "0x3A88", "0x91C0", "0x2D40", "0xB610", "0x5E08"];
        (double X, double Y)[] spots = [(70, 330), (300, 440), (520, 320), (740, 450), (560, 460), (820, 330)];

        return new StoryBuilder("DS1", "Contiguous memory wins for iteration and random access. Linked lists only win when you splice often at positions you already hold a reference to.")
            .Setup(s =>
            {
                s.Region("arr", "Array: one contiguous block", 30, 30, 940, 190);
                s.Cells("a", ["10", "20", "30", "40", "50", "60"], 70, 100, 80, 50, 4, tags: false);
                for (var i = 0; i < 6; i++)
                {
                    s.TagText("a" + i, $"0x10{i * 4:X2}");
                }

                s.Region("lst", "Linked list: nodes scattered across the heap", 30, 250, 940, 290);
                for (var i = 0; i < 6; i++)
                {
                    s.Box("n" + i, ((i + 1) * 10).ToString(), spots[i].X, spots[i].Y, 90, 46, "next ->");
                    s.TagText("n" + i, addresses[i]);
                    if (i > 0)
                    {
                        s.Connect("n" + (i - 1), "n" + i);
                    }
                }
            })
            .Code("""
                int[] array = { 10, 20, 30, 40, 50, 60 };
                int third = array[2];                 // address = base + 2 * sizeof(int)

                var list = new LinkedList<int>(array);
                var node = list.First!.Next!;         // walk the chain to get there
                list.AddAfter(node, 25);              // O(1) once you hold the node
                """)
            .Step("Same six numbers, two layouts",
                "An array stores its elements back to back. A linked list stores each element in its own node, wherever the allocator found space.",
                "Element i of an int[] lives at base + 4*i, so its address is calculated, not looked up. Each LinkedList node is a separate heap object (header, value, prev and next pointers: about 48 bytes in .NET) for 4 bytes of data.",
                [1, 4])
            .Step("Walking the array",
                "Reading a[0] brings a whole 64-byte cache line into the CPU, so the next elements are already there.",
                "An L1 hit takes about 1 ns and DRAM about 80-100 ns. The hardware prefetcher spots the sequential pattern and fetches the next lines before you ask for them.",
                [2],
                s =>
                {
                    s.Region("line", "one 64-byte cache line", 60, 50, 520, 130, Good);
                    s.PaintAll(Good, "a0", "a1", "a2", "a3", "a4", "a5");
                })
            .Step("Walking the list",
                "Each node's address is only known after the previous node has been loaded, so every hop can be a cache miss.",
                "This is called pointer chasing. The CPU cannot start the next load early, so the misses happen one after another. Both loops are O(n), but on large data sets the list is often 10-50x slower.",
                [5],
                s =>
                {
                    s.Remove("line");
                    s.PaintAll(Neutral, "a0", "a1", "a2", "a3", "a4", "a5");
                    s.PaintAll(Warn, "n0", "n1", "n2", "n3", "n4", "n5");
                    for (var i = 0; i < 5; i++)
                    {
                        s.Move("shape:n" + i, "shape:n" + (i + 1), "miss", Warn);
                    }
                })
            .Step("Insert 25 into the array",
                "Everything after the insertion point has to move one slot to the right: O(n) work.",
                "List<T>.Insert uses Array.Copy (a SIMD memmove), so each element is cheap to move, but the cost still grows with n. A full array is also reallocated and copied. Doubling the capacity keeps appends O(1) amortised.",
                [2],
                s =>
                {
                    s.PaintAll(Neutral, "n0", "n1", "n2", "n3", "n4", "n5");
                    for (var i = 5; i >= 2; i--)
                    {
                        s.Place("a" + i, Kit.CellX(70, i + 1, 80, 4), 100).Paint("a" + i, Warn);
                    }

                    s.Box("a25", "25", Kit.CellX(70, 2, 80, 4), 100, 80, 50, tone: Info);
                })
            .Step("Insert 25 into the list",
                "Only two pointers change: O(1). But you first had to walk to the right node, which is O(n) pointer chasing.",
                "This is why real code mostly uses arrays (List<T>, Vec, std::vector). Linked lists shine in intrusive designs, such as an LRU cache, where you already hold the node.",
                [4, 5],
                s =>
                {
                    for (var i = 2; i <= 5; i++)
                    {
                        s.Paint("a" + i, Neutral);
                    }

                    s.Paint("a25", Neutral);
                    s.Box("n25", "25", 410, 390, 80, 40, tone: Info);
                    s.Disconnect("n1", "n2");
                    s.Connect("n1", "n25", tone: Good).Connect("n25", "n2", tone: Good);
                    s.Say("Arrays win for scanning; lists win only for cheap splicing at a known node", Good);
                })
            .Lesson("Arrays vs linked lists: memory layout matters", "Linear structures", "Why an O(n) array scan beats an O(n) list walk, and when insertion cost flips the answer.");
    }

    // ------------------------------------------------------------------------------------------
    private static Lesson StackAndQueue()
    {
        static void Frame(SceneState s, int i, string text) => s.Box("s" + i, text, 70, 450 - i * 56, 240, 48, tone: Info);

        return new StoryBuilder("DS2", "Stacks give LIFO for free (the call stack is one). Ring buffers give FIFO without shifting: just move head and tail modulo the capacity.")
            .Setup(s =>
            {
                s.Region("stack", "Stack (last in, first out)", 30, 30, 320, 500);
                s.Region("ring", "Queue as a ring buffer (first in, first out)", 380, 30, 590, 500);
                s.Cells("r", ["", "", "", "", "", "", "", ""], 410, 200, 64, 56, 6);
                s.Box("head", "head", 410, 290, 64, 26, tone: Info);
                s.Box("tail", "tail", 410, 330, 64, 26, tone: Warn);
            })
            .Code("""
                var stack = new Stack<string>();
                stack.Push("Main"); stack.Push("Load()"); stack.Push("Parse()");
                stack.Pop();                            // "Parse()": last in, first out

                var queue = new Queue<char>(8);         // ring buffer underneath
                queue.Enqueue('A');                     // _array[_tail]; _tail = (_tail + 1) % length
                queue.Dequeue();                        // _array[_head]; _head = (_head + 1) % length
                """)
            .Step("Push three frames",
                "Each Push puts an item on top. This is exactly how method calls work: every call pushes a frame.",
                "A call frame holds the return address, saved registers and local variables. Pushing is just moving the stack-pointer register (RSP) down, so there is no allocation at all.",
                [1, 2],
                s =>
                {
                    Frame(s, 0, "Main");
                    Frame(s, 1, "Load()");
                    Frame(s, 2, "Parse()");
                })
            .Step("Pop returns the newest item",
                "Parse() returns, so its frame is removed first. Last in, first out.",
                "Each thread has its own stack (1 MB by default on Windows). Unbounded recursion exhausts it and throws StackOverflowException, which .NET cannot catch: the process dies.",
                [3],
                s => s.Remove("s2").Paint("s1", Good))
            .Step("Enqueue A, B, C, D",
                "Items are written at tail, and tail moves right.",
                "The queue is backed by a plain array. Enqueue writes one slot and bumps an index, which is O(1).",
                [5, 6],
                s =>
                {
                    s.Paint("s1", Info);
                    for (var i = 0; i < 4; i++)
                    {
                        s.Write("r" + i, ((char)('A' + i)).ToString()).Paint("r" + i, Info);
                    }

                    s.Place("tail", Kit.CellX(410, 4, 64, 6), 330);
                })
            .Step("Dequeue A and B",
                "Items leave from head. Nothing shifts: head just moves forward.",
                "A naive 'remove at index 0' on an array would shift every element, which is O(n). The ring buffer makes it O(1).",
                [7],
                s =>
                {
                    s.Write("r0", "").Paint("r0", Neutral).Write("r1", "").Paint("r1", Neutral);
                    s.Place("head", Kit.CellX(410, 2, 64, 6), 290);
                })
            .Step("Tail wraps around",
                "Enqueue E to I: after the last slot, tail wraps to slot 0 using (tail + 1) % capacity.",
                "Head and tail chase each other around the circle. The count tells you whether the buffer is empty (head == tail, count 0) or full (head == tail, count == capacity).",
                [6],
                s =>
                {
                    string[] items = ["E", "F", "G", "H"];
                    for (var i = 0; i < 4; i++)
                    {
                        s.Write("r" + (4 + i), items[i]).Paint("r" + (4 + i), Info);
                    }

                    s.Write("r0", "I").Paint("r0", Warn);
                    s.Place("tail", Kit.CellX(410, 1, 64, 6), 330);
                })
            .Step("Full: grow and unwrap",
                "When count reaches capacity, Queue<T> allocates a bigger array and copies the items in order, so head starts at 0 again.",
                "Doubling makes the copy cost amortised O(1) per item. Bounded queues refuse to grow instead, which gives back-pressure (see Resource Management).",
                [6],
                s =>
                {
                    s.Write("r1", "J").Paint("r1", Warn);
                    s.Say("Ring buffer full: double the capacity and copy in FIFO order", Info);
                })
            .Lesson("Stack and queue (ring buffer)", "Linear structures", "LIFO, the call stack, and how a circular buffer gives O(1) FIFO without shifting.");
    }

    // ------------------------------------------------------------------------------------------
    private static Lesson HashTable()
    {
        static double Row(int i, int count) => count == 4 ? 70 + i * 100 : 40 + i * 62;

        return new StoryBuilder("DS3", "Hash tables give O(1) average lookups by turning a key into an array index. Collisions are normal; the load factor and the hash quality decide whether O(1) holds.")
            .Setup(s =>
            {
                for (var i = 0; i < 4; i++)
                {
                    s.Box("b" + i, $"bucket {i}", 60, Row(i, 4), 120, 56, tone: Neutral);
                }

                s.Label("rule", "bucket = hash(key) % 4", 640, 30, 340, 15, Info);
            })
            .Code("""
                var map = new Dictionary<string, int>();
                map["cat"] = 3;                 // bucket = hash("cat") % buckets.Length
                map["emu"] = 7;                 // same bucket as "cat": chained
                bool found = map.TryGetValue("emu", out var value);
                // count > buckets.Length => resize (~2x, prime) and rehash everything
                """)
            .Step("An empty table with 4 buckets",
                "A hash table is an array of buckets. The hash of a key picks which bucket it goes into.",
                "In .NET, Dictionary<TKey,TValue> keeps two arrays: int[] buckets, and Entry[] entries holding {hashCode, next, key, value}. Collisions are chained through the 'next' index.")
            .Step("Insert \"cat\"",
                "hash(\"cat\") = 17, and 17 % 4 = 1, so it goes into bucket 1.",
                "Computing the hash costs O(key length) for a string. Picking the bucket is one modulo (or a multiply-shift trick), and the entry is appended to the entries array.",
                [2],
                s =>
                {
                    s.Box("cat", "cat : 3", 260, Row(1, 4) + 5, 120, 46, "hash 17", Info);
                    s.Connect("b1", "cat");
                })
            .Step("Insert \"dog\" and \"fox\"",
                "dog: 42 % 4 = 2. fox: 11 % 4 = 3. Each lands in its own bucket.",
                "With a good hash function, keys spread evenly, so each bucket holds about count / buckets entries. That ratio is the load factor.",
                [2],
                s =>
                {
                    s.Paint("cat", Neutral);
                    s.Box("dog", "dog : 5", 260, Row(2, 4) + 5, 120, 46, "hash 42", Info);
                    s.Box("fox", "fox : 1", 260, Row(3, 4) + 5, 120, 46, "hash 11", Info);
                    s.Connect("b2", "dog").Connect("b3", "fox");
                })
            .Step("Insert \"emu\": a collision",
                "emu: 29 % 4 = 1, the same bucket as cat. The new entry is chained after it.",
                "Collisions are unavoidable (pigeonhole principle). Chaining keeps them correct, but long chains turn O(1) into O(chain length).",
                [3],
                s =>
                {
                    s.PaintAll(Neutral, "dog", "fox");
                    s.Box("emu", "emu : 7", 440, Row(1, 4) + 5, 120, 46, "hash 29", Warn);
                    s.Connect("cat", "emu", "next");
                })
            .Step("Look up \"emu\"",
                "Hash, go to bucket 1, then walk the chain: cat does not match, emu does.",
                "Each comparison first checks the cached hash code (a cheap int compare) and only calls Equals when the hashes are equal. A custom key type with a bad GetHashCode (for example, always 0) turns the table into a linked list.",
                [4],
                s =>
                {
                    s.Paint("b1", Info).Paint("cat", Info).Paint("emu", Good);
                    s.Move("shape:b1", "shape:cat", "check");
                    s.Move("shape:cat", "shape:emu", "next");
                })
            .Step("Load factor reached: resize and rehash",
                "With 4 entries in 4 buckets the table grows to 8 buckets, and every key is placed again using % 8.",
                "Resizing is O(n), but it happens rarely (capacity roughly doubles to the next prime), so inserts stay O(1) amortised. emu now lands alone in bucket 5. Attackers who craft colliding keys can force O(n) lookups (hash flooding), which is why .NET randomises string hashing per process.",
                [5],
                s =>
                {
                    s.Write("rule", "bucket = hash(key) % 8");
                    for (var i = 0; i < 8; i++)
                    {
                        if (i < 4)
                        {
                            s.Place("b" + i, 60, Row(i, 8)).Resize("b" + i, 120, 48).Paint("b" + i, Neutral);
                        }
                        else
                        {
                            s.Box("b" + i, $"bucket {i}", 60, Row(i, 8), 120, 48);
                        }
                    }

                    s.Disconnect("cat", "emu");
                    s.Place("cat", 260, Row(1, 8) + 2).Paint("cat", Neutral);
                    s.Place("dog", 260, Row(2, 8) + 2).Paint("dog", Neutral);
                    s.Place("fox", 260, Row(3, 8) + 2).Paint("fox", Neutral);
                    s.Place("emu", 260, Row(5, 8) + 2).Paint("emu", Good);
                    s.Connect("b5", "emu");
                    s.Say("Every chain has length 1 again: O(1) average", Good);
                })
            .Lesson("Hash table: hashing, collisions and resizing", "Hashing", "How keys become array indexes, why collisions happen, and what a resize really costs.");
    }

    // ------------------------------------------------------------------------------------------
    private static Lesson BinarySearchTree()
    {
        var b = new StoryBuilder("DS4", "A BST gives O(log n) only while it stays balanced. Self-balancing trees (red-black, AVL) rotate after inserts to keep the height near log2(n).")
            .Code("""
                Node Insert(Node? node, int key)
                {
                    if (node is null) return new Node(key);
                    if (key < node.Key) node.Left = Insert(node.Left, key);
                    else                node.Right = Insert(node.Right, key);
                    return node;
                }
                """);

        var children = new Dictionary<int, (int? L, int? R)>();
        int? root = null;

        void Render(SceneState s, IEnumerable<int> path, int? highlight, Tone tone)
        {
            var pos = Kit.TreeLayout(children, root!.Value, 60, 70, 880, 95);
            s.ClearLinks();
            foreach (var (key, (x, y)) in pos)
            {
                var id = "k" + key;
                if (s.Has(id))
                {
                    s.Place(id, x - 26, y - 26).Paint(id, Neutral);
                }
                else
                {
                    s.Circle(id, key.ToString(), x, y, 26);
                }
            }

            foreach (var (key, (l, r)) in children)
            {
                if (l is { } left)
                {
                    s.Connect("k" + key, "k" + left, arrow: false);
                }

                if (r is { } right)
                {
                    s.Connect("k" + key, "k" + right, arrow: false);
                }
            }

            foreach (var p in path)
            {
                s.Paint("k" + p, Info);
            }

            if (highlight is { } h)
            {
                s.Paint("k" + h, tone);
            }
        }

        List<int> Insert(int key)
        {
            var path = new List<int>();
            if (root is null)
            {
                root = key;
                children[key] = (null, null);
                return path;
            }

            var node = root.Value;
            while (true)
            {
                path.Add(node);
                var (l, r) = children[node];
                if (key < node)
                {
                    if (l is null)
                    {
                        children[node] = (key, r);
                        break;
                    }

                    node = l.Value;
                }
                else
                {
                    if (r is null)
                    {
                        children[node] = (l, key);
                        break;
                    }

                    node = r.Value;
                }
            }

            children[key] = (null, null);
            return path;
        }

        foreach (var key in new[] { 50, 30, 70, 20, 40, 60, 80 })
        {
            var path = Insert(key);
            var route = path.Count == 0
                ? "The tree is empty, so it becomes the root."
                : string.Join(", ", path.Select(p => key < p ? $"{key} < {p}: go left" : $"{key} > {p}: go right")) + ". Attach as a new leaf.";
            b.Step($"Insert {key}", route, key == 80
                    ? "Seven keys, height 3. A balanced tree with n keys has height about log2(n), so a million keys need only about 20 comparisons."
                    : string.Empty,
                [3, 4, 5],
                s => Render(s, path, key, Good));
        }

        b.Step("Search for 60",
            "60 > 50: go right. 60 < 70: go left. Found in 3 comparisons.",
            "Each comparison throws away half of the remaining tree, just like binary search on a sorted array. Every node is a separate heap object, though, so each level can be a cache miss.",
            [4, 5],
            s => Render(s, [50, 70], 60, Good));

        b.Step("Insert sorted keys: the tree degenerates",
            "Insert 10, 20, 30, 40, 50 in order into an empty tree. Every key goes right, and the 'tree' becomes a linked list.",
            "The height is now n, so search, insert and delete are all O(n). Sorted or nearly sorted input is very common in practice (timestamps, auto-increment ids).",
            [4],
            s =>
            {
                s.RemoveWhere(sh => sh.Id.StartsWith('k'));
                s.ClearLinks();
                for (var i = 0; i < 5; i++)
                {
                    var k = (i + 1) * 10;
                    s.Circle("k" + k, k.ToString(), 120 + i * 170, 80 + i * 95, 26, tone: i == 4 ? Bad : Warn);
                    if (i > 0)
                    {
                        s.Connect("k" + (k - 10), "k" + k, arrow: false, tone: Warn);
                    }
                }

                s.Say("Height 5 for 5 keys: O(n), no better than a list", Bad);
            });

        b.Step("Self-balancing: rotations restore log n",
            "An AVL or red-black tree rotates nodes after inserts. The same five keys end up only 3 levels deep.",
            "A rotation rewires three pointers in O(1) while keeping the in-order sequence. .NET's SortedDictionary and SortedSet are red-black trees; databases prefer B-trees (see the B-tree lesson).",
            [],
            s =>
            {
                s.ClearLinks();
                (int K, double X, double Y)[] layout = [(20, 500, 80), (10, 300, 190), (40, 700, 190), (30, 600, 300), (50, 800, 300)];
                foreach (var (k, x, y) in layout)
                {
                    s.Place("k" + k, x - 26, y - 26).Paint("k" + k, Good);
                }

                s.Connect("k20", "k10", arrow: false).Connect("k20", "k40", arrow: false)
                 .Connect("k40", "k30", arrow: false).Connect("k40", "k50", arrow: false);
                s.Say("Balanced again: height 3, O(log n)", Good);
            });

        return b.Lesson("Binary search tree and balancing", "Trees", "Ordered lookups in O(log n), and how sorted input silently turns a tree into a list.");
    }

    // ------------------------------------------------------------------------------------------
    private static Lesson BinaryHeap()
    {
        var b = new StoryBuilder("DS5", "A binary heap is a complete tree stored in an array: parent of i is (i-1)/2. Insert and extract-min are O(log n), and peek is O(1).")
            .Setup(s =>
            {
                s.Caption("arrcap", "array storage", 60, 14);
                s.Cells("h", ["", "", "", "", "", "", ""], 60, 56, 66, 46, 6);
                s.Caption("treecap", "the same data viewed as a tree", 60, 140);
            })
            .Code("""
                void Insert(int value)
                {
                    heap[count] = value; int i = count++;
                    while (i > 0 && heap[(i - 1) / 2] > heap[i])       // sift up
                    { Swap(i, (i - 1) / 2); i = (i - 1) / 2; }
                }
                int ExtractMin()
                {
                    int min = heap[0]; heap[0] = heap[--count];
                    SiftDown(0);                                         // swap with the smaller child
                    return min;
                }
                """);

        var heap = new List<int>();

        void Render(SceneState s, int? active, int? other, Tone activeTone = Info)
        {
            for (var i = 0; i < 7; i++)
            {
                s.Write("h" + i, i < heap.Count ? heap[i].ToString() : "").Paint("h" + i, Neutral);
            }

            s.ClearLinks();
            for (var i = 0; i < heap.Count; i++)
            {
                var (x, y) = Kit.HeapPosition(i, 120, 210, 760, 100);
                var id = "v" + heap[i];
                if (s.Has(id))
                {
                    s.Place(id, x - 26, y - 26).Paint(id, Neutral);
                }
                else
                {
                    s.Circle(id, heap[i].ToString(), x, y, 26);
                }

                if (i > 0)
                {
                    s.Connect("v" + heap[(i - 1) / 2], id, arrow: false);
                }
            }

            foreach (var idx in new[] { active, other })
            {
                if (idx is { } k && k < heap.Count)
                {
                    s.Paint("v" + heap[k], idx == active ? activeTone : Warn).Paint("h" + k, idx == active ? activeTone : Warn);
                }
            }
        }

        foreach (var value in new[] { 40, 20, 50, 10, 30 })
        {
            heap.Add(value);
            var i = heap.Count - 1;
            b.Step($"Insert {value}", $"Append {value} at index {i}, the next free slot, which keeps the tree complete.",
                value == 40 ? "Because the tree is complete, there are no pointers: children of i are at 2i+1 and 2i+2. This keeps the heap compact and cache-friendly." : string.Empty,
                [3],
                s => Render(s, i, null));
            while (i > 0 && heap[(i - 1) / 2] > heap[i])
            {
                var p = (i - 1) / 2;
                var (child, parent) = (heap[i], heap[p]);
                (heap[i], heap[p]) = (heap[p], heap[i]);
                var current = p;
                b.Step($"Sift up: {child} < {parent}", $"{child} is smaller than its parent {parent}, so they swap.",
                    string.Empty, [4, 5], s => Render(s, current, current == 0 ? null : (current - 1) / 2));
                i = p;
            }
        }

        b.Step("Peek: the minimum is always at index 0",
            "The smallest item sits at the root, so reading it is O(1).",
            ".NET's PriorityQueue<TElement, TPriority> uses a 4-ary heap (four children per node). That makes the tree shallower and puts each node's children in the same cache line.",
            [],
            s => Render(s, 0, null, Good));

        var min = heap[0];
        heap[0] = heap[^1];
        heap.RemoveAt(heap.Count - 1);
        b.Step($"Extract-min: remove {min}",
            $"Take {min} from the root and move the last item ({heap[0]}) into its place.",
            string.Empty,
            [9],
            s =>
            {
                s.Remove("v" + min);
                Render(s, 0, null, Warn);
            });

        var j = 0;
        while (true)
        {
            var l = 2 * j + 1;
            var r = l + 1;
            var smallest = j;
            if (l < heap.Count && heap[l] < heap[smallest]) smallest = l;
            if (r < heap.Count && heap[r] < heap[smallest]) smallest = r;
            if (smallest == j) break;
            var (a, c) = (heap[j], heap[smallest]);
            (heap[j], heap[smallest]) = (heap[smallest], heap[j]);
            var at = smallest;
            b.Step($"Sift down: {a} > {c}", $"{a} is larger than its smaller child {c}, so they swap.",
                "Each level needs at most two comparisons, and there are log2(n) levels.", [10], s => Render(s, at, (at - 1) / 2));
            j = smallest;
        }

        b.Step("Heap property restored",
            "Every parent is smaller than its children again. Insert and extract both touched only one path: O(log n).",
            "Heaps power Dijkstra's algorithm, timer queues, task schedulers and top-K queries. Heapsort builds a heap and extracts n times, giving O(n log n) with no extra memory.",
            [],
            s =>
            {
                Render(s, 0, null, Good);
                s.Say("Min-heap valid: parent <= children everywhere", Good);
            });

        return b.Lesson("Binary heap and priority queues", "Trees", "A tree hidden inside an array: sift-up, sift-down, and why PriorityQueue is so fast.");
    }

    // ------------------------------------------------------------------------------------------
    private static Lesson Trie()
    {
        static void Node(SceneState s, string id, string letter, double x, double y, bool end = false) =>
            s.Circle(id, letter, x, y, 24, end ? "end of word" : null, end ? Good : Info);

        return new StoryBuilder("DS6", "A trie shares common prefixes, so lookups cost O(key length), independent of how many words are stored. That is the basis of autocomplete and IP routing tables.")
            .Setup(s => s.Circle("root", "*", 500, 60, 22, "root"))
            .Code("""
                class TrieNode { public Dictionary<char, TrieNode> Next = new(); public bool IsWord; }

                void Insert(string word)
                {
                    var node = root;
                    foreach (var ch in word)
                        node = node.Next.TryGetValue(ch, out var n) ? n : node.Next[ch] = new TrieNode();
                    node.IsWord = true;
                }
                """)
            .Step("Insert \"car\"",
                "Each character becomes a node on a path from the root. The last node is marked as the end of a word.",
                "Each node holds a small map (or a fixed 26-slot array) of children. The array version is faster, but wastes memory on empty slots.",
                [3, 4, 5, 6, 7, 8],
                s =>
                {
                    Node(s, "c", "c", 320, 150);
                    Node(s, "ca", "a", 320, 240);
                    Node(s, "car", "r", 230, 330, end: true);
                    s.Connect("root", "c").Connect("c", "ca").Connect("ca", "car");
                })
            .Step("Insert \"cat\": share the prefix",
                "\"ca\" already exists, so only one new node is needed for \"t\".",
                "Shared prefixes are where tries save both time and space compared with storing every string separately.",
                [6],
                s =>
                {
                    s.PaintAll(Neutral, "c", "ca");
                    s.Paint("car", Good);
                    Node(s, "cat", "t", 410, 330, end: true);
                    s.Connect("ca", "cat");
                })
            .Step("Insert \"cart\" and \"dog\"",
                "\"cart\" extends \"car\". \"dog\" starts a new branch from the root.",
                "A node can be both an end of a word and an inner node: \"car\" ends at r, but \"cart\" continues below it.",
                [6, 7],
                s =>
                {
                    Node(s, "cart", "t", 230, 420, end: true);
                    s.Connect("car", "cart");
                    Node(s, "d", "d", 700, 150);
                    Node(s, "do", "o", 700, 240);
                    Node(s, "dog", "g", 700, 330, end: true);
                    s.Connect("root", "d").Connect("d", "do").Connect("do", "dog");
                })
            .Step("Autocomplete \"ca\"",
                "Walk c then a (2 steps), then list every word below that node: car, cart, cat.",
                "The cost depends on the prefix length plus the number of results, not on the dictionary size. Routers use compressed binary tries (radix trees) for longest-prefix-match on IP addresses.",
                [],
                s =>
                {
                    s.PaintAll(Info, "c", "ca");
                    s.PaintAll(Good, "car", "cart", "cat");
                    s.PaintAll(Neutral, "d", "do", "dog");
                    s.Label("results", "suggestions: car, cart, cat", 560, 450, 400, 16, Good);
                })
            .Lesson("Trie (prefix tree)", "Trees", "Prefix sharing for autocomplete, spell-check and routing tables.");
    }

    // ------------------------------------------------------------------------------------------
    private static Lesson LruCache()
    {
        string[] accesses = ["A", "B", "C", "A", "D", "A", "B"];
        const int capacity = 3;
        var order = new List<string>();
        var hits = 0;
        var misses = 0;

        var b = new StoryBuilder("DS7", "An LRU cache combines a hash map (O(1) find) with a doubly linked list (O(1) move-to-front and evict-from-back). Each structure covers the other's weakness.")
            .Setup(s =>
            {
                s.Caption("seqcap", "access sequence", 60, 30);
                s.Cells("q", accesses, 60, 52, 56, 42, 6, tags: false);
                s.Caption("mru", "most recently used", 300, 180);
                s.Caption("lru", "least recently used (evicted next)", 640, 180);
                s.Table("map", "hash map: key -> node", 60, 200, 200);
                s.Label("stats", "hits 0   misses 0", 300, 330, 400, 15);
            })
            .Code("""
                public V Get(K key)
                {
                    if (_map.TryGetValue(key, out var node))
                    {
                        _list.Remove(node); _list.AddFirst(node);    // move to front: O(1)
                        return node.Value.Value;
                    }
                    if (_map.Count == Capacity)
                    {
                        _map.Remove(_list.Last!.Value.Key);          // evict LRU: O(1)
                        _list.RemoveLast();
                    }
                    /* load, then AddFirst + map add */
                }
                """);

        for (var step = 0; step < accesses.Length; step++)
        {
            var key = accesses[step];
            var hit = order.Contains(key);
            string? evicted = null;
            if (hit)
            {
                hits++;
                order.Remove(key);
            }
            else
            {
                misses++;
                if (order.Count == capacity)
                {
                    evicted = order[^1];
                    order.RemoveAt(order.Count - 1);
                }
            }

            order.Insert(0, key);
            var snapshot = order.ToArray();
            var (h, m, idx, ev) = (hits, misses, step, evicted);
            var narrative = hit
                ? $"{key} is in the map: a hit. Its node moves to the front of the list."
                : ev is null
                    ? $"{key} is not cached: a miss. Load it and insert it at the front."
                    : $"{key} is a miss and the cache is full, so the least recently used item ({ev}) is evicted.";

            b.Step($"Access {key}: {(hit ? "hit" : "miss")}", narrative,
                step == 0 ? "The list keeps recency order. The map points straight at list nodes, so no step ever scans the list." : string.Empty,
                hit ? [3, 4, 5, 6] : ev is null ? [14] : [8, 9, 10, 11, 12],
                s =>
                {
                    for (var i = 0; i < accesses.Length; i++)
                    {
                        s.Paint("q" + i, Neutral).Ghost("q" + i, i < idx);
                    }

                    s.Paint("q" + idx, hit ? Good : Warn);
                    if (ev is not null)
                    {
                        s.Place("n" + ev, 760, 420).Paint("n" + ev, Bad).Ghost("n" + ev);
                    }

                    s.RemoveWhere(sh => sh.Id.StartsWith('n') && sh.Ghost && sh.Id != "n" + ev);
                    s.ClearLinks();
                    for (var i = 0; i < snapshot.Length; i++)
                    {
                        var id = "n" + snapshot[i];
                        if (!s.Has(id))
                        {
                            s.Box(id, snapshot[i], 300 + i * 150, 210, 110, 60);
                        }

                        s.Place(id, 300 + i * 150, 210).Ghost(id, false).Paint(id, snapshot[i] == key ? (hit ? Good : Info) : Neutral);
                        if (i > 0)
                        {
                            s.Connect("n" + snapshot[i - 1], id, arrow: false);
                        }
                    }

                    s.Table("map", "hash map: key -> node", 60, 200, 200, snapshot.OrderBy(k => k).Select(k => $"{k} -> node {k}").ToArray());
                    s.Write("stats", $"hits {h}   misses {m}");
                });
        }

        b.Step("Every operation was O(1)",
            "Find via the map, reorder via the list, evict from the tail: no step scanned anything.",
            "Real caches (Redis allkeys-lru, CPU caches, the OS page cache) often approximate LRU with sampling or the CLOCK algorithm, because exact LRU bookkeeping on every access is expensive under contention.",
            [],
            s => s.Say($"{hits} hits, {misses} misses with capacity {capacity}", Good));

        return b.Lesson("LRU cache: hash map + doubly linked list", "Hashing", "The classic O(1) cache design, with eviction step by step.");
    }

    // ------------------------------------------------------------------------------------------
    private static Lesson BloomFilter()
    {
        var bits = new int[12];

        void SetBits(SceneState s, string word, int[] idx, bool write, Tone tone)
        {
            s.Box("word", word, 420, 60, 160, 50, tone: Info);
            s.ClearLinks();
            for (var k = 0; k < idx.Length; k++)
            {
                if (write)
                {
                    bits[idx[k]] = 1;
                }

                s.Connect("word", "bit" + idx[k], $"h{k + 1}", tone);
            }

            for (var i = 0; i < 12; i++)
            {
                s.Write("bit" + i, bits[i].ToString()).Paint("bit" + i, bits[i] == 1 ? Info : Neutral);
            }
        }

        return new StoryBuilder("DS8", "A Bloom filter answers 'definitely not' or 'probably yes' using a few bits per item. There are no false negatives, and the false-positive rate is tuned by its size and number of hashes.")
            .Setup(s => s.Cells("bit", Enumerable.Repeat("0", 12).ToArray(), 105, 300, 60, 52, 6))
            .NoCode()
            .Step("12 bits, 3 hash functions",
                "Start with every bit set to 0. Each item is hashed by 3 independent hash functions.",
                "The filter stores no keys at all, only bits. With about 10 bits per item and 7 hashes, the false-positive rate is about 1%.")
            .Step("Add \"apple\"",
                "Its three hashes point at bits 1, 5 and 9. Set them to 1.",
                "Adding an item is k hash computations and k bit writes: O(k), whatever the number of items already stored.",
                s => SetBits(s, "apple", [1, 5, 9], true, Info))
            .Step("Add \"pear\"",
                "Its hashes point at bits 3, 5 and 10. Bit 5 is shared with apple, which is fine.",
                "Bits are never cleared, so a plain Bloom filter cannot delete items. Counting Bloom filters and cuckoo filters can.",
                s => SetBits(s, "pear", [3, 5, 10], true, Info))
            .Step("Query \"grape\": definitely not present",
                "Its hashes point at bits 2, 5 and 9. Bit 2 is 0, so grape was never added.",
                "A single 0 bit proves absence, so there are no false negatives. Databases (RocksDB, Cassandra, HBase) use this to skip reading files from disk that cannot contain a key.",
                s =>
                {
                    SetBits(s, "grape", [2, 5, 9], false, Warn);
                    s.Paint("bit2", Bad).Paint("word", Good);
                    s.Say("A zero bit means definitely NOT in the set", Good);
                })
            .Step("Query \"plum\": a false positive",
                "Its hashes land on bits 1, 3 and 10, which were all set by apple and pear. The filter says 'probably yes', but plum was never added.",
                "The false-positive probability is about (1 - e^(-k*n/m))^k, for n items in m bits with k hashes. Callers must confirm a 'yes' against the real data.",
                s =>
                {
                    SetBits(s, "plum", [1, 3, 10], false, Warn);
                    s.PaintAll(Warn, "bit1", "bit3", "bit10", "word");
                    s.Say("All bits set by other items: probably present, but wrong", Warn);
                })
            .Lesson("Bloom filter", "Hashing", "A probabilistic set that saves disk reads, with no false negatives and a few false positives.");
    }

    // ------------------------------------------------------------------------------------------
    private static Lesson BTree() =>
        new StoryBuilder("DS9", "B-trees put hundreds of keys in each node so a lookup touches only 3-4 pages. That is why databases and file systems use them instead of binary trees.")
            .NoCode()
            .Step("Storage reads whole pages",
                "SSDs and disks read in pages of 4-16 KB. Reading one key costs the same as reading a whole page.",
                "Random read latency is about 100 us on an NVMe SSD and about 10 ms on a spinning disk, which is 1,000 to 100,000 times slower than RAM. The number of page reads is what matters.",
                s =>
                {
                    s.Region("page", "one disk page = 8 KB", 300, 160, 400, 200, Info);
                    s.Label("keys", "fits about 500 keys + child pointers", 340, 240, 340, 16, Info);
                })
            .Step("A binary tree over 1 million keys",
                "Height about log2(1,000,000) = 20. Each level is a separate node, likely on a different page: up to 20 page reads per lookup.",
                "Even if the top levels are cached, the bottom levels of a big tree are not. 20 random reads at 100 us is 2 ms per lookup.",
                s =>
                {
                    s.Remove("page", "keys");
                    for (var i = 0; i < 6; i++)
                    {
                        s.Circle("bt" + i, i == 5 ? "..." : $"L{i + 1}", 200 + i * 110, 80 + i * 75, 24, tone: Warn);
                        if (i > 0)
                        {
                            s.Connect("bt" + (i - 1), "bt" + i, arrow: false);
                        }
                    }

                    s.Label("h", "height ~ 20 levels -> ~20 page reads", 560, 470, 420, 15, Warn);
                })
            .Step("A B-tree: wide nodes",
                "Each node holds many sorted keys and many children. With 500 keys per node, 1 million keys fit in 3 levels.",
                "The root and the internal levels are tiny and stay cached in RAM, so a lookup typically costs a single disk read for the leaf.",
                s =>
                {
                    s.RemoveWhere(sh => sh.Id.StartsWith("bt", StringComparison.Ordinal));
                    s.Remove("h");
                    s.ClearLinks();
                    s.Box("root", "100 | 200 | 300", 380, 70, 240, 50, "root page");
                    string[] kids = ["10 | 40 | 70", "120 | 150 | 180", "220 | 250 | 280", "320 | 360 | 390"];
                    for (var i = 0; i < 4; i++)
                    {
                        s.Box("c" + i, kids[i], 40 + i * 240, 230, 200, 50, "leaf page");
                        s.Connect("root", "c" + i);
                    }

                    s.Label("h", "1,000,000 keys / 500 per node -> 3 levels", 300, 360, 500, 15, Good);
                })
            .Step("Look up 250",
                "In the root, 200 < 250 < 300, so follow the third child. Inside the leaf, binary search finds 250.",
                "The search inside a node is in RAM and is cheap (binary search over a sorted array). The expensive part is the page reads, which have been reduced from about 20 to about 3.",
                s =>
                {
                    s.Paint("root", Info).Paint("c2", Good);
                    s.RetoneLinks(Info, l => l.To == "c2");
                    s.Move("shape:root", "shape:c2", "1 page read");
                })
            .Step("Insert into a full node: split",
                "When a leaf overflows it splits in two and pushes its middle key up to the parent. The tree only grows at the root, so every leaf stays at the same depth.",
                "B+ trees keep all values in the leaves and link the leaves together for fast range scans. SQL Server, PostgreSQL, MySQL InnoDB, NTFS, ext4 and APFS are all built on B-tree variants.",
                s =>
                {
                    s.RetoneLinks(Neutral);
                    s.PaintAll(Neutral, "root", "c2");
                    s.Write("c2", "220 | 240").Paint("c2", Good);
                    s.Box("c2b", "260 | 280", 520, 320, 160, 50, "new leaf", Good);
                    s.Write("root", "100 | 200 | 250 | 300").Paint("root", Info);
                    s.Connect("root", "c2b");
                    s.Say("Balanced by construction: all leaves at the same depth", Good);
                })
            .Lesson("B-tree: why databases use wide nodes", "Trees", "Designing for page reads: how fan-out turns 20 disk reads into 3.");
}
