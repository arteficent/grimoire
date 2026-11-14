using Avalonia;
using Avalonia.Styling;

namespace grimoire.Rendering;

/// <summary>
/// The two paper themes: white paper with dark pens (light) and gray paper with light pens (dark).
/// The choice is remembered between runs.
/// </summary>
public static class Theme
{
    private static readonly string StorePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "grimoire", "theme");

    public static bool IsDark => Palette.IsDark;

    public static void Load()
    {
        var dark = false;
        try
        {
            dark = File.Exists(StorePath) && File.ReadAllText(StorePath).Trim() == "dark";
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Unreadable preferences fall back to white paper.
        }

        Apply(dark);
    }

    public static void Set(bool dark)
    {
        Apply(dark);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(StorePath)!);
            File.WriteAllText(StorePath, dark ? "dark" : "light");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Not being able to remember the theme is harmless.
        }
    }

    private static void Apply(bool dark)
    {
        Palette.UseDark(dark);
        if (Application.Current is { } app)
        {
            app.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
        }
    }
}

/// <summary>Asks every visible doodle and ink frame to draw itself onto the page again (used on navigation).</summary>
public static class InkReplay
{
    public static event Action? Requested;

    public static void Request() => Requested?.Invoke();
}
