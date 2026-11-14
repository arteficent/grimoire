namespace grimoire.Model;

public enum ShapeKind
{
    Box,
    Circle,
    Label,
    Table,
    Region
}

/// <summary>A generic diagram element positioned in a fixed 1000 x 560 virtual canvas.</summary>
public sealed class Shape
{
    public required string Id { get; init; }
    public ShapeKind Kind { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public double W { get; set; }
    public double H { get; set; }
    public string? Text { get; set; }
    public string? Sub { get; set; }
    public string? Tag { get; set; }
    public Tone Tone { get; set; }

    /// <summary>Palette index used as fill colour; -1 means use the tone.</summary>
    public int Color { get; set; } = -1;

    /// <summary>Dashed outline and faded fill, e.g. freed memory or a not-yet-existing node.</summary>
    public bool Ghost { get; set; }

    /// <summary>0..1 progress/occupancy bar drawn inside a box.</summary>
    public double Fill { get; set; }

    public double FontSize { get; set; } = 13;
    public bool Mono { get; set; }
    public List<string> Rows { get; private set; } = [];
    public HashSet<int> HotRows { get; private set; } = [];

    public Shape Clone()
    {
        var copy = (Shape)MemberwiseClone();
        copy.Rows = [.. Rows];
        copy.HotRows = [.. HotRows];
        return copy;
    }
}

public sealed record Link(
    string From,
    string To,
    string? Label = null,
    Tone Tone = Tone.Neutral,
    bool Dashed = false,
    bool Arrow = true,
    bool Animate = false);
