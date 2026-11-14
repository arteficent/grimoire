using grimoire.Model;
using static grimoire.Model.Tone;

namespace grimoire.Stories;

internal static class AlgorithmLessons
{
    public static IEnumerable<Lesson> All()
    {
        yield return BigO();
        yield return BinarySearch();
        yield return InsertionSort();
        yield return MergeSort();
        yield return QuickSort();
        yield return GraphTraversal();
        yield return Dijkstra();
        yield return TopologicalSort();
        yield return DynamicProgramming();
    }

    // ------------------------------------------------------------------------------------------
    private static Lesson BigO()
    {
        (string Id, string Name)[] classes = [("o1", "O(1)"), ("olog", "O(log n)"), ("on", "O(n)"), ("onlog", "O(n log n)"), ("on2", "O(n^2)")];

        void Show(SceneState s, int n, double[] heights, string[] subs, Tone[] tones)
        {
            for (var i = 0; i < classes.Length; i++)
            {
                var h = Math.Max(26, heights[i]);
                var x = 90 + i * 175;
                if (!s.Has(classes[i].Id))
                {
                    s.Box(classes[i].Id, classes[i].Name, x, 470 - h, 120, h);
                }

                s.Place(classes[i].Id, x, 470 - h).Resize(classes[i].Id, 120, h).SubText(classes[i].Id, subs[i]).Paint(classes[i].Id, tones[i]);
            }

            s.Label("n", $"n = {n:N0}", 40, 30, 400, 20, Info);
        }

        return new StoryBuilder("AL1", "Big-O describes how cost grows, not how fast code is. Pick the right growth class first, then let constants and caches decide among equals.")
            .NoCode()
            .Step("Five growth rates at n = 16",
                "Each bar shows how many basic operations an algorithm in that class performs for 16 items.",
                "O(1): a hash lookup. O(log n): binary search. O(n): one scan. O(n log n): a good sort. O(n^2): comparing every pair.",
                s => Show(s, 16, [4, 16, 64, 256 * 0.6, 256 * 1.5], ["1", "4", "16", "64", "256"], [Good, Good, Info, Info, Warn]))
            .Step("Now n = 1,024",
                "Multiply the input by 64. O(n^2) grows to about a million operations and dwarfs everything else.",
                "At 1 ns per operation, the quadratic algorithm takes about 1 ms while the others take microseconds. At n = 1,000,000 it would take about 17 minutes, compared with about 20 ms for O(n log n).",
                s => Show(s, 1024, [2, 4, 8, 40, 420], ["1", "10", "1,024", "10,240", "1,048,576"], [Good, Good, Good, Info, Bad]))
            .Step("Constants and caches still matter",
                "For small n, a simple O(n) scan over an array can beat an O(log n) tree because it never misses the cache.",
                "Big-O hides constant factors. A contiguous scan runs at memory bandwidth (tens of GB/s), while each tree level can cost a 100 ns cache miss. Library sorts switch to insertion sort for small slices for exactly this reason.",
                s => Show(s, 32, [10, 50, 120, 200, 380], ["array scan wins here", "tree: cache misses", "", "", ""], [Neutral, Warn, Good, Neutral, Neutral]))
            .Step("Amortised cost",
                "List<T>.Add is O(1) amortised: most appends are cheap, and an occasional resize copies everything.",
                "Doubling the capacity means n appends cost at most about 2n copies in total, which is O(1) per append on average. Worst-case latency still spikes at a resize, which matters for real-time systems.",
                s =>
                {
                    s.Write("n", "appends to a doubling array");
                    double[] costs = [26, 26, 60, 26, 110];
                    string[] subs = ["cheap", "cheap", "resize: copy 4", "cheap", "resize: copy 8"];
                    for (var i = 0; i < classes.Length; i++)
                    {
                        s.Place(classes[i].Id, 90 + i * 175, 470 - costs[i]).Resize(classes[i].Id, 120, costs[i])
                         .Write(classes[i].Id, $"add #{i + 1}", subs[i]).Paint(classes[i].Id, costs[i] > 26 ? Warn : Good);
                    }
                })
            .Lesson("Big-O intuition: how cost grows", "Foundations", "Why O(n^2) explodes, and why constants and caches still decide the winner among equals.");
    }

    // ------------------------------------------------------------------------------------------
    private static Lesson BinarySearch()
    {
        int[] data = [3, 8, 12, 17, 21, 26, 31, 37, 42, 48, 55, 61];
        const int target = 37;
        const double x0 = 40, w = 72, gap = 6;

        var b = new StoryBuilder("AL2", "Binary search halves the remaining range each step: O(log n). It needs sorted data and random access, and the classic bug is computing mid as (lo + hi) / 2.")
            .Setup(s =>
            {
                s.Cells("c", data.Select(d => d.ToString()).ToArray(), x0, 200, w, 56, gap);
                s.Box("lo", "lo", x0, 275, w, 26, tone: Info);
                s.Box("mid", "mid", x0, 305, w, 26, tone: Warn);
                s.Box("hi", "hi", Kit.CellX(x0, 11, w, gap), 275, w, 26, tone: Info);
                s.Label("goal", $"find {target}", 40, 60, 300, 22, Info);
            })
            .Code("""
                int lo = 0, hi = a.Length - 1;
                while (lo <= hi)
                {
                    int mid = lo + (hi - lo) / 2;      // not (lo + hi) / 2: that can overflow
                    if (a[mid] == target) return mid;
                    if (a[mid] < target) lo = mid + 1;
                    else                 hi = mid - 1;
                }
                return ~lo;                            // .NET: bitwise complement = insertion point
                """);

        b.Step("Search a sorted array",
            $"Look for {target} among 12 sorted numbers. lo and hi mark the range that could still contain it.",
            "A linear scan would check up to 12 items. Binary search needs at most floor(log2 12) + 1 = 4 probes. For a billion items it needs 30.",
            [1]);

        int lo = 0, hi = data.Length - 1, probe = 0;
        while (lo <= hi)
        {
            var mid = lo + (hi - lo) / 2;
            probe++;
            var (l, h, m) = (lo, hi, mid);
            if (data[mid] == target)
            {
                b.Step($"Probe {probe}: a[{m}] = {data[m]}: found",
                    $"The middle element is {target}. Done after {probe} comparisons.",
                    "Each probe reads one element far from the previous one, which is a likely cache miss on big arrays. Cache-friendly layouts (Eytzinger order) put probes close together in memory.",
                    [4, 5],
                    s =>
                    {
                        s.Place("mid", Kit.CellX(x0, m, w, gap), 305);
                        s.Paint("c" + m, Good);
                        s.Say($"Found {target} at index {m} in {probe} probes", Good);
                    });
                break;
            }

            var goRight = data[mid] < target;
            b.Step($"Probe {probe}: a[{m}] = {data[m]}",
                goRight
                    ? $"{data[m]} < {target}, so the target must be to the right. Discard the left half."
                    : $"{data[m]} > {target}, so the target must be to the left. Discard the right half.",
                probe == 1 ? "mid = lo + (hi - lo) / 2 avoids integer overflow when lo + hi exceeds int.MaxValue. That bug sat in the JDK's binary search for about 9 years." : string.Empty,
                [4, goRight ? 6 : 7],
                s =>
                {
                    s.Place("mid", Kit.CellX(x0, m, w, gap), 305);
                    for (var i = 0; i < data.Length; i++)
                    {
                        var outside = goRight ? i <= m : i >= m;
                        if (i < l || i > h || outside)
                        {
                            s.Ghost("c" + i).Paint("c" + i, Neutral);
                        }
                    }

                    s.Paint("c" + m, Warn);
                    if (goRight)
                    {
                        s.Place("lo", Kit.CellX(x0, m + 1, w, gap), 275);
                    }
                    else
                    {
                        s.Place("hi", Kit.CellX(x0, m - 1, w, gap), 275);
                    }
                });
            if (goRight) lo = mid + 1; else hi = mid - 1;
        }

        return b.Lesson("Binary search", "Searching", "Halving the search space, the overflow bug, and what 'not found' returns.");
    }

    // ------------------------------------------------------------------------------------------
    private static Lesson InsertionSort()
    {
        var a = new List<int> { 5, 2, 8, 1, 3 };
        const double x0 = 230, w = 90, gap = 20, baseY = 480, unit = 40;

        void Bar(SceneState s, int value, int index, bool raised, Tone tone)
        {
            var h = value * unit;
            var id = "bar" + value;
            var y = baseY - h - (raised ? 90 : 0);
            if (!s.Has(id))
            {
                s.Box(id, value.ToString(), x0 + index * (w + gap), y, w, h);
            }

            s.Place(id, x0 + index * (w + gap), y).Paint(id, tone);
        }

        var b = new StoryBuilder("AL3", "Insertion sort is O(n^2) in general but O(n) on nearly sorted data, with tiny constants. That is why production sorts use it for small slices.")
            .Code("""
                for (int i = 1; i < a.Length; i++)
                {
                    int key = a[i], j = i - 1;
                    while (j >= 0 && a[j] > key)
                    {
                        a[j + 1] = a[j];        // shift right
                        j--;
                    }
                    a[j + 1] = key;             // drop the key into the gap
                }
                """);

        b.Step("Unsorted input", "Grow a sorted prefix from the left, one item at a time.",
            "Like sorting a hand of playing cards: pick up the next card and slide it left into place.",
            [1],
            s =>
            {
                for (var i = 0; i < a.Count; i++)
                {
                    Bar(s, a[i], i, false, Neutral);
                }
            });

        for (var i = 1; i < a.Count; i++)
        {
            var key = a[i];
            var j = i - 1;
            var sortedEnd = i - 1;
            b.Step($"Pick key {key}", $"Lift {key} out. Everything to its left (green) is already sorted.", string.Empty, [3],
                s =>
                {
                    for (var k = 0; k <= sortedEnd; k++)
                    {
                        Bar(s, a[k], k, false, Good);
                    }

                    Bar(s, key, sortedEnd + 1, true, Warn);
                });

            var shifted = false;
            while (j >= 0 && a[j] > key)
            {
                var moving = a[j];
                var hole = j;
                a[j + 1] = a[j];
                b.Step($"{moving} > {key}: shift right", $"{moving} is larger than the key, so it slides one slot to the right.",
                    shifted ? string.Empty : "Each shift is a plain memory move to the neighbouring slot. The data stays in cache and the access pattern is predictable for the CPU.",
                    [4, 5, 6],
                    s =>
                    {
                        Bar(s, moving, hole + 1, false, Info);
                        Bar(s, key, hole, true, Warn);
                    });
                shifted = true;
                j--;
            }

            a[j + 1] = key;
            var slot = j + 1;
            var snapshot = a.ToArray();
            var last = i;
            b.Step(shifted ? $"Insert {key} at index {slot}" : $"{key} is already in place",
                shifted ? $"Drop {key} into the gap. The sorted prefix grows by one." : $"The left neighbour is not larger, so no shifting is needed: one comparison only.",
                string.Empty,
                [8],
                s =>
                {
                    for (var k = 0; k <= last; k++)
                    {
                        Bar(s, snapshot[k], k, false, Good);
                    }
                });
        }

        b.Step("Sorted",
            "Worst case (reverse order) needs about n^2 / 2 shifts. Nearly sorted input needs about n.",
            ".NET's Array.Sort (introsort) and Python's Timsort both switch to insertion sort for small slices (16 elements or fewer in .NET), because it beats O(n log n) algorithms at that size.",
            [],
            s => s.Say("Sorted: 1 2 3 5 8", Good));

        return b.Lesson("Insertion sort", "Sorting", "The simple quadratic sort that every fast library sort still uses for small slices.");
    }

    // ------------------------------------------------------------------------------------------
    private static Lesson MergeSort()
    {
        var a = new[] { 38, 27, 43, 3, 9, 82, 10, 15 };
        const double x0 = 80, w = 95, gap = 15;

        void Layout(SceneState s, int[] arr, int lo, int mid, int hi, bool merged)
        {
            for (var i = 0; i < arr.Length; i++)
            {
                var id = "m" + arr[i];
                if (!s.Has(id))
                {
                    s.Box(id, arr[i].ToString(), x0 + i * (w + gap), 200, w, 60);
                    s.Shape(id).Mono = true;
                }

                var inRange = i >= lo && i < hi;
                var tone = !inRange ? Neutral : merged ? Good : i < mid ? Info : Warn;
                s.Place(id, x0 + i * (w + gap), inRange && merged ? 200 : inRange ? 320 : 200).Paint(id, tone);
            }
        }

        var b = new StoryBuilder("AL4", "Merge sort is always O(n log n) and stable, at the cost of an O(n) buffer. Its sequential access pattern makes it the basis of external (disk) sorting and Timsort.")
            .Code("""
                for (int width = 1; width < n; width *= 2)
                    for (int lo = 0; lo < n - width; lo += 2 * width)
                    {
                        int mid = lo + width, hi = Math.Min(lo + 2 * width, n);
                        Merge(a, lo, mid, hi, buffer);   // two sorted runs -> one sorted run
                    }
                """);

        b.Step("Every single item is a sorted run",
            "Bottom-up merge sort starts with runs of length 1 and repeatedly merges neighbouring runs.",
            "There are log2(8) = 3 passes, and each pass touches all n items. That gives O(n log n) total, whatever the input looks like.",
            [1],
            s => Layout(s, a, 0, 0, 0, false));

        for (var width = 1; width < a.Length; width *= 2)
        {
            for (var lo = 0; lo < a.Length - width; lo += 2 * width)
            {
                var mid = lo + width;
                var hi = Math.Min(lo + 2 * width, a.Length);
                var before = a.ToArray();
                var left = a[lo..mid];
                var right = a[mid..hi];
                var merged = left.Concat(right).OrderBy(v => v).ToArray();
                merged.CopyTo(a, lo);
                var after = a.ToArray();
                var (l, m, h, wd) = (lo, mid, hi, width);
                b.Step($"Merge [{string.Join(", ", left)}] + [{string.Join(", ", right)}]",
                    $"Compare the heads of both runs and repeatedly take the smaller one, giving [{string.Join(", ", merged)}].",
                    lo == 0 && width == 1
                        ? "Merging reads both runs sequentially and writes to a temporary buffer, which is ideal for caches and disks. Taking from the left run on ties keeps the sort stable (equal keys keep their order)."
                        : string.Empty,
                    [4, 5],
                    s =>
                    {
                        Layout(s, before, l, m, h, false);
                        Layout(s, after, l, m, h, true);
                        s.Label("pass", $"pass with run width {wd}", 80, 60, 400, 16, Info);
                    });
            }
        }

        b.Step("Sorted in 3 passes",
            "Three passes over 8 items: about n log2 n = 24 element moves.",
            "Databases sort data larger than RAM by sorting chunks and then doing a k-way merge of the sorted files (external merge sort). Timsort (Python, Java objects) detects runs that are already sorted and merges them, which is fast on real-world data.",
            [],
            s =>
            {
                Layout(s, a, 0, 0, a.Length, true);
                s.Say("Sorted: 3 9 10 15 27 38 43 82", Good);
            });

        return b.Lesson("Merge sort", "Sorting", "Divide into runs, merge in order: guaranteed O(n log n), stable, and disk friendly.");
    }

    // ------------------------------------------------------------------------------------------
    private static Lesson QuickSort()
    {
        var a = new List<int> { 6, 3, 8, 1, 5, 9, 2, 7 };
        const double x0 = 120, w = 80, gap = 18, baseY = 480, unit = 38;
        var final = new HashSet<int>();

        void Render(SceneState s, int lo, int hi, int? pivotIndex, params int[] swapped)
        {
            for (var i = 0; i < a.Count; i++)
            {
                var v = a[i];
                var id = "bar" + v;
                var h = v * unit;
                if (!s.Has(id))
                {
                    s.Box(id, v.ToString(), x0 + i * (w + gap), baseY - h, w, h);
                }

                var tone = final.Contains(v) ? Good
                    : i == pivotIndex ? Warn
                    : swapped.Contains(i) ? Info
                    : Neutral;
                s.Place(id, x0 + i * (w + gap), baseY - h).Paint(id, tone).Ghost(id, !final.Contains(v) && (i < lo || i > hi));
            }
        }

        var b = new StoryBuilder("AL5", "Quicksort partitions around a pivot in place. It averages O(n log n) with excellent cache behaviour, but bad pivots give O(n^2), so real implementations randomise or switch to heapsort.")
            .Code("""
                int Partition(int[] a, int lo, int hi)        // Lomuto
                {
                    int pivot = a[hi], i = lo - 1;
                    for (int j = lo; j < hi; j++)
                        if (a[j] < pivot) Swap(a, ++i, j);     // grow the "< pivot" zone
                    Swap(a, i + 1, hi);                       // pivot lands in its final slot
                    return i + 1;
                }
                """);

        b.Step("Unsorted input", "Quicksort picks a pivot and moves smaller items to its left and larger items to its right.",
            "Unlike merge sort, it needs no extra buffer: the partition step swaps items in place.",
            [1],
            s => Render(s, 0, a.Count - 1, null));

        var ranges = new Stack<(int Lo, int Hi)>();
        ranges.Push((0, a.Count - 1));
        while (ranges.Count > 0)
        {
            var (lo, hi) = ranges.Pop();
            if (lo > hi)
            {
                continue;
            }

            if (lo == hi)
            {
                final.Add(a[lo]);
                continue;
            }

            var pivot = a[hi];
            var (pl, ph) = (lo, hi);
            b.Step($"Partition [{lo}..{hi}] around {pivot}",
                $"The pivot is the last element of the range, {pivot}. Items outside the range are faded.",
                pl == 0 && ph == a.Count - 1 ? "Choosing the last element is simple, but already-sorted input then gives the worst case. Introsort (.NET) uses median-of-three and falls back to heapsort when recursion gets too deep." : string.Empty,
                [3],
                s => Render(s, pl, ph, ph));

            var i = lo - 1;
            for (var j = lo; j < hi; j++)
            {
                if (a[j] >= pivot)
                {
                    continue;
                }

                i++;
                if (i == j)
                {
                    continue;
                }

                (a[i], a[j]) = (a[j], a[i]);
                var (si, sj) = (i, j);
                b.Step($"{a[si]} < {pivot}: swap into the left zone",
                    $"{a[si]} is smaller than the pivot, so it swaps with {a[sj]} at the edge of the 'smaller than pivot' zone.",
                    string.Empty, [5],
                    s => Render(s, pl, ph, ph, si, sj));
            }

            (a[i + 1], a[hi]) = (a[hi], a[i + 1]);
            var p = i + 1;
            final.Add(pivot);
            b.Step($"Pivot {pivot} lands at index {p}",
                $"Everything left of {pivot} is smaller and everything right is larger, so {pivot} is in its final position.",
                string.Empty, [6, 7],
                s => Render(s, pl, ph, null, p));

            ranges.Push((p + 1, hi));
            ranges.Push((lo, p - 1));
        }

        b.Step("Sorted",
            "Each partition fixed one pivot and split the problem in two.",
            "With balanced splits there are about log2 n levels of recursion, each doing O(n) work. Partitioning scans memory sequentially, which is why quicksort usually beats heapsort in practice despite the same Big-O.",
            [],
            s =>
            {
                foreach (var v in a)
                {
                    final.Add(v);
                }

                Render(s, 0, a.Count - 1, null);
                s.Say("Sorted in place: 1 2 3 5 6 7 8 9", Good);
            });

        return b.Lesson("Quicksort", "Sorting", "In-place partitioning around a pivot, and why pivot choice decides between n log n and n^2.");
    }

    // ------------------------------------------------------------------------------------------
    private static readonly (string Id, double X, double Y)[] GraphNodes =
        [("A", 500, 80), ("B", 300, 190), ("C", 700, 190), ("D", 180, 320), ("E", 420, 320), ("F", 700, 320), ("G", 560, 430)];

    private static readonly (string A, string B)[] GraphEdges =
        [("A", "B"), ("A", "C"), ("B", "D"), ("B", "E"), ("C", "F"), ("E", "G"), ("F", "G")];

    private static IEnumerable<string> Neighbours(string node) =>
        GraphEdges.Where(e => e.A == node).Select(e => e.B)
            .Concat(GraphEdges.Where(e => e.B == node).Select(e => e.A))
            .OrderBy(n => n);

    private static void DrawGraph(SceneState s)
    {
        foreach (var (id, x, y) in GraphNodes)
        {
            s.Circle(id, id, x, y, 26);
        }

        foreach (var (a, b) in GraphEdges)
        {
            s.Connect(a, b, arrow: false);
        }
    }

    private static Lesson GraphTraversal()
    {
        var b = new StoryBuilder("AL6", "BFS explores level by level with a queue and finds shortest paths in unweighted graphs. DFS dives deep with a stack (or recursion) and suits cycle detection and topological order.")
            .Setup(s =>
            {
                DrawGraph(s);
                s.Caption("fcap", "frontier", 40, 470);
            })
            .Code("""
                var queue = new Queue<Node>([start]);
                var seen = new HashSet<Node> { start };
                while (queue.Count > 0)
                {
                    var node = queue.Dequeue();          // DFS: Stack.Pop() instead
                    foreach (var next in node.Neighbours)
                        if (seen.Add(next)) queue.Enqueue(next);
                }
                """);

        void Frontier(SceneState s, IReadOnlyList<string> items, string kind)
        {
            s.RemoveWhere(sh => sh.Id.StartsWith("fq", StringComparison.Ordinal));
            s.Write("fcap", kind);
            for (var i = 0; i < items.Count; i++)
            {
                s.Box("fq" + i, items[i], 40 + i * 64, 495, 56, 40, tone: Warn);
            }
        }

        // BFS
        var dist = new Dictionary<string, int> { ["A"] = 0 };
        var queue = new List<string> { "A" };
        var visited = new List<string>();
        b.Step("Breadth-first search from A",
            "Put A in the queue. BFS always takes the oldest discovered node next.",
            "With an adjacency list, BFS visits every node and edge once: O(V + E).",
            [1, 2],
            s =>
            {
                s.Paint("A", Warn).SubText("A", "d=0");
                Frontier(s, queue, "queue (first in, first out)");
            });

        while (queue.Count > 0)
        {
            var node = queue[0];
            queue.RemoveAt(0);
            visited.Add(node);
            var discovered = new List<string>();
            foreach (var n in Neighbours(node))
            {
                if (!dist.ContainsKey(n))
                {
                    dist[n] = dist[node] + 1;
                    queue.Add(n);
                    discovered.Add(n);
                }
            }

            var (snapQueue, snapVisited, snapDist, current) = (queue.ToArray(), visited.ToArray(), new Dictionary<string, int>(dist), node);
            b.Step($"BFS: visit {node}",
                discovered.Count > 0
                    ? $"Dequeue {node}. Discover {string.Join(", ", discovered)} at distance {dist[node] + 1} and add them to the back of the queue."
                    : $"Dequeue {node}. All its neighbours have already been seen.",
                string.Empty,
                [5, 6, 7],
                s =>
                {
                    foreach (var (id, _, _) in GraphNodes)
                    {
                        var tone = id == current ? Info : snapVisited.Contains(id) ? Good : snapQueue.Contains(id) ? Warn : Neutral;
                        s.Paint(id, tone).SubText(id, snapDist.TryGetValue(id, out var d) ? $"d={d}" : null);
                    }

                    Frontier(s, snapQueue, "queue (first in, first out)");
                });
        }

        b.Step("BFS result: shortest hop counts",
            "Each node was reached by the fewest possible edges from A. G is 3 hops away.",
            "This is how social networks compute 'degrees of separation', how a garbage collector can mark reachable objects, and how routers flood link-state updates.",
            [],
            s => s.Say("BFS order: A B C D E F G (level by level)", Good));

        // DFS
        var order = new List<string>();
        var stackTrace = new List<string>();
        void Dfs(string node, List<string> path)
        {
            order.Add(node);
            path.Add(node);
            var (snapOrder, snapPath, current) = (order.ToArray(), path.ToArray(), node);
            b.Step($"DFS: visit {node}",
                order.Count == 1
                    ? "Depth-first search: go as deep as possible before backing up. The call stack remembers where to return."
                    : $"Go deeper from {snapPath[^2]} to {node}.",
                order.Count == 1 ? "A recursive DFS on a very deep graph can overflow the thread's stack. Production code often uses an explicit Stack<T> instead." : string.Empty,
                [5],
                s =>
                {
                    foreach (var (id, _, _) in GraphNodes)
                    {
                        var tone = id == current ? Info : snapPath.Contains(id) ? Warn : snapOrder.Contains(id) ? Good : Neutral;
                        s.Paint(id, tone).SubText(id, null);
                    }

                    Frontier(s, snapPath, "stack (recursion path)");
                });

            foreach (var n in Neighbours(node))
            {
                if (!order.Contains(n))
                {
                    Dfs(n, path);
                }
            }

            path.RemoveAt(path.Count - 1);
        }

        Dfs("A", stackTrace);

        b.Step("DFS result",
            "DFS visited A B D E G F C: it finished whole branches before moving sideways.",
            "DFS uses memory proportional to the depth, while BFS can hold a whole level at once. DFS is the basis of cycle detection, topological sort, finding strongly connected components (Tarjan) and maze solving.",
            [],
            s =>
            {
                foreach (var (id, _, _) in GraphNodes)
                {
                    s.Paint(id, Good);
                }

                Frontier(s, [], "stack (recursion path)");
                s.Say("DFS order: A B D E G F C (branch by branch)", Good);
            });

        return b.Lesson("Graph traversal: BFS and DFS", "Graphs", "Queue versus stack: level-by-level exploration versus diving deep.");
    }

    // ------------------------------------------------------------------------------------------
    private static Lesson Dijkstra()
    {
        (string Id, double X, double Y)[] nodes = [("S", 90, 280), ("A", 320, 110), ("B", 320, 450), ("C", 560, 280), ("D", 790, 110), ("T", 890, 420)];
        (string A, string B, int W)[] edges = [("S", "A", 4), ("S", "B", 1), ("B", "A", 2), ("A", "C", 1), ("B", "C", 5), ("C", "D", 3), ("D", "T", 1), ("C", "T", 6)];

        var dist = nodes.ToDictionary(n => n.Id, _ => int.MaxValue);
        var prev = new Dictionary<string, string>();
        var done = new HashSet<string>();
        dist["S"] = 0;

        string D(string id) => dist[id] == int.MaxValue ? "inf" : dist[id].ToString();

        var b = new StoryBuilder("AL7", "Dijkstra repeatedly settles the closest unsettled node and relaxes its edges. With a binary heap it runs in O((V + E) log V). It needs non-negative weights; use Bellman-Ford otherwise.")
            .Setup(s =>
            {
                foreach (var (id, x, y) in nodes)
                {
                    s.Circle(id, id, x, y, 28, "d=inf");
                }

                foreach (var (a, c, w) in edges)
                {
                    s.Connect(a, c, w.ToString(), arrow: false);
                }

                s.SubText("S", "d=0").Paint("S", Warn);
            })
            .Code("""
                dist[source] = 0; pq.Enqueue(source, 0);
                while (pq.TryDequeue(out var u, out var d))
                {
                    if (d > dist[u]) continue;                  // stale entry
                    foreach (var (v, w) in graph[u])
                        if (dist[u] + w < dist[v])               // relax
                        {
                            dist[v] = dist[u] + w; prev[v] = u;
                            pq.Enqueue(v, dist[v]);
                        }
                }
                """);

        b.Step("Find the cheapest route from S to T",
            "Every distance starts at infinity except the source, which is 0.",
            "Think of it as road distances or network link costs. The priority queue always hands out the closest node that has not been settled yet.",
            [1]);

        while (done.Count < nodes.Length)
        {
            var u = dist.Where(kv => !done.Contains(kv.Key) && kv.Value != int.MaxValue).OrderBy(kv => kv.Value).First().Key;
            done.Add(u);
            var improved = new List<string>();
            foreach (var (a, c, w) in edges)
            {
                var v = a == u ? c : c == u ? a : null;
                if (v is null || done.Contains(v))
                {
                    continue;
                }

                if (dist[u] + w < dist[v])
                {
                    improved.Add($"{v}: {D(v)} -> {dist[u] + w}");
                    dist[v] = dist[u] + w;
                    prev[v] = u;
                }
            }

            var snapDist = nodes.ToDictionary(n => n.Id, n => D(n.Id));
            var snapDone = done.ToHashSet();
            var current = u;
            b.Step($"Settle {u} (distance {D(u)})",
                improved.Count > 0
                    ? $"{u} is the closest unsettled node. Relaxing its edges improves {string.Join(", ", improved)}."
                    : $"{u} is the closest unsettled node. None of its edges improves a distance.",
                u == "B" ? "Going through B makes A cheaper (1 + 2 = 3 instead of 4). A settled node's distance is final, because all weights are non-negative, so any detour can only add cost." : string.Empty,
                [2, 5, 6, 7],
                s =>
                {
                    s.RetoneLinks(Neutral);
                    foreach (var (id, _, _) in nodes)
                    {
                        s.SubText(id, "d=" + snapDist[id]).Paint(id, id == current ? Info : snapDone.Contains(id) ? Good : snapDist[id] == "inf" ? Neutral : Warn);
                    }

                    s.RetoneLinks(Info, l => l.From == current || l.To == current);
                });
        }

        var path = new List<string> { "T" };
        while (prev.TryGetValue(path[^1], out var p))
        {
            path.Add(p);
        }

        path.Reverse();
        b.Step("Shortest path S to T",
            $"Follow the 'previous' pointers back from T: {string.Join(" -> ", path)}, total cost {dist["T"]}.",
            "The direct-looking edges (S-A = 4, C-T = 6) lose to longer chains of cheap hops. GPS navigation, OSPF routing and game AI (A*, which adds a heuristic) are all built on this algorithm.",
            [],
            s =>
            {
                s.RetoneLinks(Neutral);
                for (var i = 1; i < path.Count; i++)
                {
                    var (x, y) = (path[i - 1], path[i]);
                    s.RetoneLinks(Good, l => (l.From == x && l.To == y) || (l.From == y && l.To == x));
                }

                foreach (var id in path)
                {
                    s.Paint(id, Good);
                }

                s.Say($"Shortest path {string.Join(" -> ", path)} = {dist["T"]}", Good);
            });

        return b.Lesson("Dijkstra's shortest path", "Graphs", "Settling the closest node first: how GPS and network routing find cheapest routes.");
    }

    // ------------------------------------------------------------------------------------------
    private static Lesson TopologicalSort()
    {
        (string Id, string Name, double X, double Y)[] tasks =
        [
            ("restore", "restore", 110, 140), ("docs", "docs", 110, 400), ("codegen", "codegen", 320, 70),
            ("lib", "build lib", 400, 230), ("app", "build app", 630, 140), ("tests", "run tests", 630, 330), ("pack", "pack", 860, 240)
        ];
        (string From, string To)[] deps =
        [
            ("restore", "codegen"), ("restore", "lib"), ("codegen", "lib"), ("lib", "app"),
            ("lib", "tests"), ("app", "pack"), ("tests", "pack"), ("docs", "pack")
        ];

        var indeg = tasks.ToDictionary(t => t.Id, t => deps.Count(d => d.To == t.Id));
        var output = new List<string>();
        var ready = new List<string>(tasks.Where(t => indeg[t.Id] == 0).Select(t => t.Id));

        var b = new StoryBuilder("AL8", "Topological sort orders tasks so every dependency comes first. Kahn's algorithm repeatedly emits nodes with no remaining inputs; if some are never emitted, there is a cycle.")
            .Setup(s =>
            {
                foreach (var (id, name, x, y) in tasks)
                {
                    s.Box(id, name, x - 70, y - 26, 140, 52, $"in = {indeg[id]}");
                }

                foreach (var (f, t) in deps)
                {
                    s.Connect(f, t);
                }

                s.Caption("outcap", "build order", 40, 470);
            })
            .Code("""
                var ready = new Queue<Task>(tasks.Where(t => inDegree[t] == 0));
                while (ready.Count > 0)
                {
                    var t = ready.Dequeue(); order.Add(t);
                    foreach (var next in t.Dependents)
                        if (--inDegree[next] == 0) ready.Enqueue(next);
                }
                if (order.Count < tasks.Count) throw new InvalidOperationException("cycle!");
                """);

        void Paint(SceneState s, string? current, IReadOnlyCollection<string> readySet, IReadOnlyList<string> done, Dictionary<string, int> deg)
        {
            foreach (var (id, _, _, _) in tasks)
            {
                s.SubText(id, $"in = {deg[id]}").Paint(id, id == current ? Info : done.Contains(id) ? Good : readySet.Contains(id) ? Warn : Neutral);
            }

            for (var i = 0; i < done.Count; i++)
            {
                s.Box("o" + i, tasks.First(t => t.Id == done[i]).Name, 40 + i * 132, 495, 124, 40, tone: Good);
            }
        }

        b.Step("A build with dependencies",
            "Arrows point from a task to the tasks that need it. 'in' counts unfinished prerequisites.",
            "Build systems (MSBuild, Bazel, make), package managers, schedulers and spreadsheet recalculation all need an order like this. It only exists if the graph has no cycles (a DAG).",
            [1],
            s => Paint(s, null, ready, output, new Dictionary<string, int>(indeg)));

        while (ready.Count > 0)
        {
            var t = ready[0];
            ready.RemoveAt(0);
            output.Add(t);
            var unlocked = new List<string>();
            foreach (var (f, to) in deps.Where(d => d.From == t))
            {
                if (--indeg[to] == 0)
                {
                    ready.Add(to);
                    unlocked.Add(tasks.First(x => x.Id == to).Name);
                }
            }

            var (snapReady, snapOut, snapDeg, current) = (ready.ToArray(), output.ToArray(), new Dictionary<string, int>(indeg), t);
            b.Step($"Run {tasks.First(x => x.Id == t).Name}",
                unlocked.Count > 0
                    ? $"It has no remaining prerequisites. Finishing it unlocks: {string.Join(", ", unlocked)}."
                    : "It has no remaining prerequisites. Nothing new is unlocked yet.",
                t == "docs" ? "Several tasks can be ready at the same time (amber). Those can run in parallel, which is how build systems use every CPU core." : string.Empty,
                [4, 5, 6],
                s => Paint(s, current, snapReady, snapOut, snapDeg));
        }

        b.Step("A valid order",
            "Every task ran after all of its prerequisites.",
            "If a cycle existed (say pack -> restore), those tasks would never reach in = 0 and would be missing from the output. That is how tools report circular dependencies.",
            [8],
            s => s.Say("All 7 tasks ordered, no cycles", Good));

        return b.Lesson("Topological sort (Kahn's algorithm)", "Graphs", "Ordering builds and jobs by their dependencies, and detecting cycles.");
    }

    // ------------------------------------------------------------------------------------------
    private static Lesson DynamicProgramming()
    {
        var nodes = new List<(string Id, int N, double X, double Y, string? Parent)>();
        var counter = 0;
        var leaves = 0;

        double Build(int n, int depth, string? parent)
        {
            var id = "f" + counter++;
            var index = nodes.Count;
            nodes.Add((id, n, 0, 70 + depth * 95, parent));
            double x;
            if (n < 2)
            {
                x = 60 + leaves++ * 110;
            }
            else
            {
                var l = Build(n - 1, depth + 1, id);
                var r = Build(n - 2, depth + 1, id);
                x = (l + r) / 2;
            }

            nodes[index] = nodes[index] with { X = x };
            return x;
        }

        Build(5, 0, null);
        var duplicates = nodes.GroupBy(n => n.N).Where(g => g.Count() > 1 && g.Key >= 2).SelectMany(g => g.Skip(1)).Select(n => n.Id).ToHashSet();

        return new StoryBuilder("AL9", "Dynamic programming = recursion + reuse. When subproblems overlap, store each answer once (memoise) or fill a table bottom-up, and exponential time becomes linear.")
            .Setup(s =>
            {
                foreach (var (id, n, x, y, parent) in nodes)
                {
                    s.Circle(id, $"f({n})", x, y, 25);
                    s.Shape(id).FontSize = 11;
                    if (parent is not null)
                    {
                        s.Connect(parent, id, arrow: false);
                    }
                }
            })
            .Code("""
                int Fib(int n) => n < 2 ? n : Fib(n - 1) + Fib(n - 2);       // exponential

                int FibMemo(int n, Dictionary<int, int> memo) =>
                    n < 2 ? n : memo.TryGetValue(n, out var v) ? v
                              : memo[n] = FibMemo(n - 1, memo) + FibMemo(n - 2, memo);

                int FibTable(int n) { int a = 0, b = 1; for (int i = 0; i < n; i++) (a, b) = (b, a + b); return a; }
                """)
            .Step("Naive recursion for fib(5)",
                "fib(n) = fib(n-1) + fib(n-2). The call tree for fib(5) already has 15 calls.",
                "The number of calls grows like 1.618^n, so fib(50) needs about 40 billion calls. The recursion keeps solving the same small problems over and over.",
                [1])
            .Step("Spot the repeated work",
                "fib(3) is computed twice and fib(2) three times. Amber nodes are duplicates.",
                "Overlapping subproblems plus optimal substructure (the answer is built from sub-answers) are the two signs that DP applies.",
                [1],
                s =>
                {
                    foreach (var id in duplicates)
                    {
                        s.Paint(id, Warn);
                    }
                })
            .Step("Memoisation: compute each value once",
                "Cache every result the first time it is computed. Every duplicate becomes an O(1) lookup, and its whole subtree disappears.",
                "This is top-down DP. The number of distinct subproblems is n + 1, so time becomes O(n). The cost is O(n) memory for the cache plus the recursion depth.",
                [3, 4, 5],
                s =>
                {
                    var pruned = nodes.Where(n => n.Parent is not null && (duplicates.Contains(n.Parent) || IsUnder(n.Parent))).Select(n => n.Id).ToArray();
                    s.Remove(pruned);
                    foreach (var id in duplicates.Where(s.Has))
                    {
                        s.Paint(id, Good).SubText(id, "cache hit");
                    }

                    bool IsUnder(string? parent)
                    {
                        while (parent is not null)
                        {
                            if (duplicates.Contains(parent))
                            {
                                return true;
                            }

                            parent = nodes.First(n => n.Id == parent).Parent;
                        }

                        return false;
                    }
                })
            .Step("Bottom-up table",
                "Or skip recursion entirely: fill fib(0), fib(1), ..., fib(5) left to right. Each cell needs only the two before it.",
                "Bottom-up DP has no recursion (so no stack overflow) and a predictable memory access pattern. Here only two variables are needed, giving O(n) time and O(1) memory.",
                [7],
                s =>
                {
                    s.RemoveWhere(sh => sh.Id.StartsWith('f'));
                    s.ClearLinks();
                    int[] fib = [0, 1, 1, 2, 3, 5];
                    s.Cells("t", fib.Select(v => v.ToString()).ToArray(), 220, 240, 80, 60, 10, tone: Good);
                    for (var i = 0; i < 6; i++)
                    {
                        s.TagText("t" + i, $"fib({i})");
                    }

                    s.Say("15 calls became 6 table cells", Good);
                })
            .Lesson("Dynamic programming: memoisation and tables", "Techniques", "Turning exponential recursion into linear time by reusing sub-results.");
    }
}
