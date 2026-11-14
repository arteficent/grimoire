namespace grimoire.Model;

/// <summary>
/// Immutable-by-convention snapshot of everything drawn for one step. The story builder clones the
/// previous snapshot and mutates it through the helpers below.
/// </summary>
public sealed class SceneState
{
    public List<ThreadNode> Threads { get; private set; } = [];
    public List<ResourceNode> Resources { get; private set; } = [];
    public List<CoreNode> Cores { get; private set; } = [];
    public List<MemCell> Memory { get; private set; } = [];
    public List<Transfer> Transfers { get; private set; } = [];
    public List<Shape> Shapes { get; private set; } = [];
    public List<Link> Links { get; private set; } = [];
    public HashSet<string> ForcedDeadlock { get; private set; } = [];
    public string? Banner { get; set; }
    public Tone BannerTone { get; set; }

    /// <summary>Region of the virtual diagram canvas worth showing; shared by every step of a lesson.</summary>
    public (double X, double Y, double W, double H)? Frame { get; set; }

    public SceneState Clone()
    {
        var copy = (SceneState)MemberwiseClone();
        copy.Threads = Threads.Select(t => t.Clone()).ToList();
        copy.Resources = Resources.Select(r => r.Clone()).ToList();
        copy.Cores = Cores.Select(c => c.Clone()).ToList();
        copy.Memory = [.. Memory];
        copy.Transfers = [.. Transfers];
        copy.Shapes = Shapes.Select(s => s.Clone()).ToList();
        copy.Links = [.. Links];
        copy.ForcedDeadlock = [.. ForcedDeadlock];
        return copy;
    }

    /// <summary>Clears per-step decorations so they do not leak into the next step.</summary>
    public void ClearTransient()
    {
        Transfers.Clear();
        Banner = null;
        BannerTone = Tone.Neutral;
        foreach (var t in Threads)
        {
            t.Note = null;
        }

        foreach (var c in Cores)
        {
            c.Note = null;
        }
    }

    // ---- construction ---------------------------------------------------------------------

    public ThreadNode AddThread(string id, string name)
    {
        var node = new ThreadNode { Id = id, Name = name, Color = Threads.Count };
        Threads.Add(node);
        return node;
    }

    public ResourceNode AddResource(string id, string label, ResourceKind kind, double x, double y, string? caption = null, string? value = null)
    {
        var node = new ResourceNode { Id = id, Label = label, Kind = kind, X = x, Y = y, Caption = caption, Value = value };
        Resources.Add(node);
        return node;
    }

    public SceneState AddCores(int count)
    {
        for (var i = 0; i < count; i++)
        {
            Cores.Add(new CoreNode { Index = i });
        }

        return this;
    }

    // ---- lookup ---------------------------------------------------------------------------

    public ThreadNode T(string id) => Threads.First(t => t.Id == id);

    public ResourceNode R(string id) => Resources.First(r => r.Id == id);

    public CoreNode Core(int index) => Cores.First(c => c.Index == index);

    // ---- thread / lock state --------------------------------------------------------------

    public SceneState Run(string thread, int core)
    {
        Unschedule(thread);
        Core(core).ThreadId = thread;
        var t = T(thread);
        if (t.Phase != ThreadPhase.Spinning)
        {
            t.Phase = ThreadPhase.Running;
        }

        return this;
    }

    public SceneState Unschedule(string thread)
    {
        foreach (var c in Cores.Where(c => c.ThreadId == thread))
        {
            c.ThreadId = null;
            c.Register = null;
        }

        return this;
    }

    public SceneState Acquire(string thread, string resource)
    {
        var r = R(resource);
        r.Waiters.Remove(thread);
        if (!r.Owners.Contains(thread))
        {
            r.Owners.Add(thread);
        }

        var t = T(thread);
        t.At = resource;
        t.Phase = ThreadPhase.Running;
        return this;
    }

    public SceneState Release(string thread, string resource)
    {
        R(resource).Owners.Remove(thread);
        var t = T(thread);
        if (t.At == resource)
        {
            t.At = null;
        }

        return this;
    }

    /// <summary>Thread queues on a resource. Phase decides how it waits (spin, kernel block, timed wait).</summary>
    public SceneState WaitOn(string thread, string resource, ThreadPhase phase = ThreadPhase.Blocked)
    {
        var r = R(resource);
        if (!r.Waiters.Contains(thread))
        {
            r.Waiters.Add(thread);
        }

        var t = T(thread);
        t.At = resource;
        t.Phase = phase;
        return this;
    }

    public SceneState StopWaiting(string thread, string resource)
    {
        R(resource).Waiters.Remove(thread);
        var t = T(thread);
        if (t.At == resource)
        {
            t.At = null;
        }

        return this;
    }

    public SceneState Go(string thread, string? resource, ThreadPhase? phase = null)
    {
        var t = T(thread);
        t.At = resource;
        if (phase is { } p)
        {
            t.Phase = p;
        }

        return this;
    }

    public SceneState Phase(string thread, ThreadPhase phase)
    {
        T(thread).Phase = phase;
        if (phase is ThreadPhase.Done or ThreadPhase.Faulted)
        {
            Unschedule(thread);
        }

        return this;
    }

    public SceneState Note(string thread, string text)
    {
        T(thread).Note = text;
        return this;
    }

    public SceneState Rename(string thread, string name)
    {
        T(thread).Name = name;
        return this;
    }

    // ---- resource content -----------------------------------------------------------------

    public SceneState Set(string resource, string? value, Tone? tone = null)
    {
        var r = R(resource);
        r.Value = value;
        if (tone is { } t)
        {
            r.Tone = t;
        }

        return this;
    }

    public SceneState Mark(string resource, Tone tone)
    {
        R(resource).Tone = tone;
        return this;
    }

    public SceneState Items(string resource, params string[] items)
    {
        var r = R(resource);
        r.Items.Clear();
        r.Items.AddRange(items);
        return this;
    }

    public SceneState Push(string resource, string item)
    {
        R(resource).Items.Add(item);
        return this;
    }

    public SceneState Pop(string resource, string? item = null)
    {
        var r = R(resource);
        if (item is null)
        {
            if (r.Items.Count > 0)
            {
                r.Items.RemoveAt(0);
            }
        }
        else
        {
            r.Items.Remove(item);
        }

        return this;
    }

    // ---- hardware -------------------------------------------------------------------------

    public SceneState Cache(int core, string key, string value, char state)
    {
        var c = Core(core);
        var index = c.Cache.FindIndex(l => l.Key == key);
        var line = new CacheLine(key, value, state);
        if (index >= 0)
        {
            c.Cache[index] = line;
        }
        else
        {
            c.Cache.Add(line);
        }

        return this;
    }

    public SceneState Invalidate(int core, string key)
    {
        var c = Core(core);
        var index = c.Cache.FindIndex(l => l.Key == key);
        if (index >= 0)
        {
            c.Cache[index] = c.Cache[index] with { State = 'I' };
        }

        return this;
    }

    public SceneState Reg(int core, string? text)
    {
        Core(core).Register = text;
        return this;
    }

    public SceneState CoreNote(int core, string text)
    {
        Core(core).Note = text;
        return this;
    }

    public SceneState Ram(string key, string value, Tone tone = Tone.Neutral)
    {
        var index = Memory.FindIndex(m => m.Key == key);
        var cell = new MemCell(key, value, tone);
        if (index >= 0)
        {
            Memory[index] = cell;
        }
        else
        {
            Memory.Add(cell);
        }

        return this;
    }

    public SceneState Move(string from, string to, string label, Tone tone = Tone.Info)
    {
        Transfers.Add(new Transfer(from, to, label, tone));
        return this;
    }

    // ---- diagram shapes -------------------------------------------------------------------

    public Shape Shape(string id) => Shapes.First(s => s.Id == id);

    public bool Has(string id) => Shapes.Any(s => s.Id == id);

    public Shape Put(string id, ShapeKind kind, double x, double y, double w, double h, string? text = null, string? sub = null, Tone tone = Tone.Neutral)
    {
        var existing = Shapes.FirstOrDefault(s => s.Id == id);
        var shape = existing ?? new Shape { Id = id };
        shape.Kind = kind;
        shape.X = x;
        shape.Y = y;
        shape.W = w;
        shape.H = h;
        shape.Text = text;
        shape.Sub = sub;
        shape.Tone = tone;
        if (existing is null)
        {
            Shapes.Add(shape);
        }

        return shape;
    }

    public SceneState Box(string id, string? text, double x, double y, double w = 90, double h = 50, string? sub = null, Tone tone = Tone.Neutral)
    {
        Put(id, ShapeKind.Box, x, y, w, h, text, sub, tone);
        return this;
    }

    public SceneState Circle(string id, string? text, double cx, double cy, double r = 24, string? sub = null, Tone tone = Tone.Neutral)
    {
        Put(id, ShapeKind.Circle, cx - r, cy - r, r * 2, r * 2, text, sub, tone);
        return this;
    }

    public SceneState Label(string id, string text, double x, double y, double w = 400, double size = 13, Tone tone = Tone.Neutral)
    {
        Put(id, ShapeKind.Label, x, y, w, size * 1.6, text, null, tone).FontSize = size;
        return this;
    }

    public SceneState Region(string id, string title, double x, double y, double w, double h, Tone tone = Tone.Neutral)
    {
        Put(id, ShapeKind.Region, x, y, w, h, title, null, tone);
        return this;
    }

    public SceneState Table(string id, string title, double x, double y, double w, params string[] rows)
    {
        var shape = Put(id, ShapeKind.Table, x, y, w, 30 + rows.Length * 24, title);
        shape.Mono = true;
        shape.Rows.Clear();
        shape.Rows.AddRange(rows);
        shape.HotRows.Clear();
        return this;
    }

    public SceneState HotRow(string id, params int[] rows)
    {
        var shape = Shape(id);
        shape.HotRows.Clear();
        foreach (var r in rows)
        {
            shape.HotRows.Add(r);
        }

        return this;
    }

    public SceneState Paint(string id, Tone tone)
    {
        Shape(id).Tone = tone;
        return this;
    }

    public SceneState PaintAll(Tone tone, params string[] ids)
    {
        foreach (var id in ids)
        {
            Shape(id).Tone = tone;
        }

        return this;
    }

    public SceneState Write(string id, string? text, string? sub = null)
    {
        var shape = Shape(id);
        shape.Text = text;
        if (sub is not null)
        {
            shape.Sub = sub;
        }

        return this;
    }

    public SceneState SubText(string id, string? sub)
    {
        Shape(id).Sub = sub;
        return this;
    }

    public SceneState TagText(string id, string? tag)
    {
        Shape(id).Tag = tag;
        return this;
    }

    public SceneState Place(string id, double x, double y)
    {
        var shape = Shape(id);
        shape.X = x;
        shape.Y = y;
        return this;
    }

    public SceneState Resize(string id, double w, double h)
    {
        var shape = Shape(id);
        shape.W = w;
        shape.H = h;
        return this;
    }

    public SceneState Ghost(string id, bool ghost = true)
    {
        Shape(id).Ghost = ghost;
        return this;
    }

    public SceneState Tint(string id, int color)
    {
        Shape(id).Color = color;
        return this;
    }

    public SceneState FillTo(string id, double fill)
    {
        Shape(id).Fill = Math.Clamp(fill, 0, 1);
        return this;
    }

    public SceneState Remove(params string[] ids)
    {
        Shapes.RemoveAll(s => ids.Contains(s.Id));
        Links.RemoveAll(l => ids.Contains(l.From) || ids.Contains(l.To));
        return this;
    }

    public SceneState RemoveWhere(Func<Shape, bool> predicate)
    {
        var ids = Shapes.Where(predicate).Select(s => s.Id).ToArray();
        return Remove(ids);
    }

    public SceneState Connect(string from, string to, string? label = null, Tone tone = Tone.Neutral, bool dashed = false, bool arrow = true, bool animate = false)
    {
        Links.RemoveAll(l => l.From == from && l.To == to);
        Links.Add(new Link(from, to, label, tone, dashed, arrow, animate));
        return this;
    }

    public SceneState Disconnect(string from, string to)
    {
        Links.RemoveAll(l => l.From == from && l.To == to);
        return this;
    }

    public SceneState ClearLinks()
    {
        Links.Clear();
        return this;
    }

    public SceneState RetoneLinks(Tone tone, Func<Link, bool>? where = null)
    {
        for (var i = 0; i < Links.Count; i++)
        {
            if (where is null || where(Links[i]))
            {
                Links[i] = Links[i] with { Tone = tone };
            }
        }

        return this;
    }

    // ---- narrative ------------------------------------------------------------------------

    public SceneState Say(string banner, Tone tone)
    {
        Banner = banner;
        BannerTone = tone;
        return this;
    }

    public SceneState Deadlock(params string[] threads)
    {
        ForcedDeadlock.Clear();
        foreach (var t in threads)
        {
            ForcedDeadlock.Add(t);
        }

        return this;
    }

    public SceneState ClearDeadlock()
    {
        ForcedDeadlock.Clear();
        return this;
    }

    /// <summary>Threads on a cycle of the wait-for graph (thread waits on a resource owned by another thread).</summary>
    public HashSet<string> FindDeadlockedThreads()
    {
        var result = new HashSet<string>(ForcedDeadlock);
        var waitsFor = new Dictionary<string, List<string>>();
        foreach (var r in Resources)
        {
            foreach (var w in r.Waiters)
            {
                if (!waitsFor.TryGetValue(w, out var list))
                {
                    waitsFor[w] = list = [];
                }

                list.AddRange(r.Owners);
            }
        }

        foreach (var start in waitsFor.Keys)
        {
            // DFS from start; if we come back to start it is on a cycle.
            var stack = new Stack<string>();
            var seen = new HashSet<string>();
            stack.Push(start);
            while (stack.Count > 0)
            {
                var current = stack.Pop();
                if (!waitsFor.TryGetValue(current, out var next))
                {
                    continue;
                }

                foreach (var n in next)
                {
                    if (n == start)
                    {
                        result.Add(start);
                        stack.Clear();
                        break;
                    }

                    if (seen.Add(n))
                    {
                        stack.Push(n);
                    }
                }
            }
        }

        return result;
    }
}
