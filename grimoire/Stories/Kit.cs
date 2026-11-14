using grimoire.Model;

namespace grimoire.Stories;

/// <summary>Layout helpers shared by the diagram lessons (virtual canvas is 1000 x 560).</summary>
internal static class Kit
{
    public const double CellW = 60;
    public const double CellH = 46;
    public const double CellGap = 6;

    public static double CellX(double x0, int index, double w = CellW, double gap = CellGap) => x0 + index * (w + gap);

    /// <summary>A row of boxes with ids prefix0..prefixN and their index as a tag.</summary>
    public static SceneState Cells(this SceneState s, string prefix, IReadOnlyList<string> values, double x, double y,
        double w = CellW, double h = CellH, double gap = CellGap, bool tags = true, Tone tone = Tone.Neutral)
    {
        for (var i = 0; i < values.Count; i++)
        {
            var id = prefix + i;
            s.Box(id, values[i], CellX(x, i, w, gap), y, w, h, tone: tone);
            s.Shape(id).Mono = true;
            s.TagText(id, tags ? i.ToString() : null);
        }

        return s;
    }

    public static SceneState Caption(this SceneState s, string id, string text, double x, double y, double w = 320) =>
        s.Label(id, text, x, y, w, 11.5);

    /// <summary>Bar chart keyed by value so that reordering animates bars sliding.</summary>
    public static SceneState Bars(this SceneState s, IReadOnlyList<int> values, double x0, double baseY, double w, double unit, double gap = 10)
    {
        for (var i = 0; i < values.Count; i++)
        {
            var v = values[i];
            var id = "bar" + v;
            var h = Math.Max(24, v * unit);
            if (s.Has(id))
            {
                s.Place(id, x0 + i * (w + gap), baseY - h).Resize(id, w, h);
            }
            else
            {
                s.Box(id, v.ToString(), x0 + i * (w + gap), baseY - h, w, h);
            }
        }

        return s;
    }

    /// <summary>In-order x / depth y positions for a binary tree.</summary>
    public static Dictionary<int, (double X, double Y)> TreeLayout(
        IReadOnlyDictionary<int, (int? Left, int? Right)> children, int root, double left, double top, double width, double levelGap)
    {
        var order = new List<(int Key, int Depth)>();
        void Walk(int? node, int depth)
        {
            if (node is not { } n)
            {
                return;
            }

            var (l, r) = children.TryGetValue(n, out var c) ? c : (null, null);
            Walk(l, depth + 1);
            order.Add((n, depth));
            Walk(r, depth + 1);
        }

        Walk(root, 0);
        var result = new Dictionary<int, (double, double)>();
        for (var i = 0; i < order.Count; i++)
        {
            result[order[i].Key] = (left + (i + 0.5) * width / order.Count, top + order[i].Depth * levelGap);
        }

        return result;
    }

    /// <summary>Position of array index i in a complete binary tree (heap layout).</summary>
    public static (double X, double Y) HeapPosition(int index, double left, double top, double width, double levelGap)
    {
        var depth = (int)Math.Floor(Math.Log2(index + 1));
        var first = (1 << depth) - 1;
        var slots = 1 << depth;
        var pos = index - first;
        return (left + (pos + 0.5) * width / slots, top + depth * levelGap);
    }

    /// <summary>Gantt chart slices: boxes g0..gN plus a time axis.</summary>
    public static SceneState Gantt(this SceneState s, string prefix, IReadOnlyList<(string Label, int Start, int Length, int Color)> slices,
        double x0, double y, double unit, double h = 46)
    {
        s.RemoveWhere(sh => sh.Id.StartsWith(prefix, StringComparison.Ordinal));
        var end = 0;
        for (var i = 0; i < slices.Count; i++)
        {
            var (label, start, length, color) = slices[i];
            var id = $"{prefix}{i}";
            s.Box(id, label, x0 + start * unit, y, Math.Max(4, length * unit - 2), h);
            s.Tint(id, color);
            end = Math.Max(end, start + length);
        }

        for (var t = 0; t <= end; t++)
        {
            if (t % 2 == 0 || t == end)
            {
                s.Label($"{prefix}t{t}", t.ToString(), x0 + t * unit - 4, y + h + 6, 30, 10.5);
            }
        }

        return s;
    }

    /// <summary>A row of adjacent fields (instruction encodings, packet headers, float layouts) sized by weight.</summary>
    public static SceneState Fields(this SceneState s, string prefix, IReadOnlyList<(string Name, string Value, double Weight)> fields,
        double x, double y, double totalWidth, double h = 60, Tone tone = Tone.Neutral)
    {
        var sum = fields.Sum(f => f.Weight);
        var cursor = x;
        for (var i = 0; i < fields.Count; i++)
        {
            var w = totalWidth * fields[i].Weight / sum;
            var id = prefix + i;
            s.Box(id, fields[i].Value, cursor, y, w - 3, h, fields[i].Name, tone);
            s.Shape(id).Mono = true;
            s.Shape(id).FontSize = 12;
            cursor += w;
        }

        return s;
    }

    /// <summary>Labelled bars; with log = true the height follows log10 of the value.</summary>
    public static SceneState LabeledBars(this SceneState s, string prefix, IReadOnlyList<(string Label, double Value, string Text)> items,
        double x0, double baseY, double w, double gap, double maxH, bool log = false, double logMin = 1)
    {
        var max = items.Max(i => log ? Math.Log10(i.Value / logMin) : i.Value);
        for (var i = 0; i < items.Count; i++)
        {
            var (label, value, text) = items[i];
            var v = log ? Math.Log10(Math.Max(logMin, value) / logMin) : value;
            var h = Math.Max(22, maxH * v / max);
            var id = prefix + i;
            if (s.Has(id))
            {
                s.Place(id, x0 + i * (w + gap), baseY - h).Resize(id, w, h).Write(id, text);
            }
            else
            {
                s.Box(id, text, x0 + i * (w + gap), baseY - h, w, h);
            }

            s.Shape(id).FontSize = 11;
            s.Label(prefix + "l" + i, label, x0 + i * (w + gap), baseY + 6, w + gap, 10.5);
        }

        return s;
    }

    /// <summary>Removes every shape and link, for steps that switch to a new picture.</summary>
    public static SceneState Wipe(this SceneState s)
    {
        s.ClearLinks();
        return s.RemoveWhere(_ => true);
    }

    public const double SeqLeft = 200;
    public const double SeqRight = 800;

    /// <summary>Two sequence-diagram participants with vertical lifelines.</summary>
    public static SceneState Lifelines(this SceneState s, string left, string right, double bottom = 530)
    {
        s.Box("lc", left, SeqLeft - 100, 30, 200, 50, tone: Tone.Info);
        s.Box("ls", right, SeqRight - 100, 30, 200, 50, tone: Tone.Info);
        s.Box("lcl", null, SeqLeft - 2, 80, 4, bottom - 80);
        return s.Box("lsl", null, SeqRight - 2, 80, 4, bottom - 80);
    }

    /// <summary>A sequence-diagram message: a slanted arrow from one lifeline to the other (or lost halfway).</summary>
    public static SceneState Msg(this SceneState s, string id, bool toRight, double y, string label, Tone tone = Tone.Info, bool lost = false)
    {
        var (x1, x2) = toRight ? (SeqLeft, SeqRight) : (SeqRight, SeqLeft);
        s.Box(id + "a", null, x1 - 6, y - 6, 12, 12, tone: tone);
        if (lost)
        {
            s.Box(id + "b", "X", (x1 + x2) / 2 - 14, y + 6, 28, 28, tone: Tone.Bad);
        }
        else
        {
            s.Box(id + "b", null, x2 - 6, y + 34, 12, 12, tone: tone);
        }

        return s.Connect(id + "a", id + "b", label, lost ? Tone.Bad : tone);
    }

    public static SceneState ClearMessages(this SceneState s) => s.RemoveWhere(sh => sh.Id.StartsWith("msg", StringComparison.Ordinal));

    /// <summary>A small filled circle in a palette colour (scatter points, neurons, tokens).</summary>
    public static SceneState Dot(this SceneState s, string id, double cx, double cy, int color, double r = 7, string? sub = null)
    {
        s.Circle(id, null, cx, cy, r, sub);
        return s.Tint(id, color);
    }

    /// <summary>A line through points, drawn as links between 2 px anchors; re-calling it with new points morphs the curve.</summary>
    public static SceneState Polyline(this SceneState s, string prefix, IReadOnlyList<(double X, double Y)> points, Tone tone = Tone.Info, bool dashed = false)
    {
        var anchor = prefix + "p";
        s.RemoveWhere(sh => sh.Id.StartsWith(anchor, StringComparison.Ordinal)
            && int.TryParse(sh.Id.AsSpan(anchor.Length), out var n) && n >= points.Count);
        for (var i = 0; i < points.Count; i++)
        {
            s.Box(anchor + i, null, points[i].X - 1, points[i].Y - 1, 2, 2, tone: tone);
            if (i > 0)
            {
                s.Connect(anchor + (i - 1), anchor + i, tone: tone, dashed: dashed, arrow: false);
            }
        }

        return s;
    }

    /// <summary>X and Y axis lines with captions; the plot area is (x, y, w, h).</summary>
    public static SceneState Axes(this SceneState s, string prefix, double x, double y, double w, double h, string xLabel, string yLabel)
    {
        s.Box(prefix + "ax", null, x, y + h, w, 2);
        s.Box(prefix + "ay", null, x, y, 2, h);
        s.Label(prefix + "xl", xLabel, x + w - 220, y + h + 8, 220, 11);
        return s.Label(prefix + "yl", yLabel, x + 10, y - 6, 300, 11);
    }
}
