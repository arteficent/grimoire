using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Fonts;

namespace grimoire;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args) =>
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
                  .UsePlatformDetect()
                  .WithInterFont()
                  .ConfigureFonts(fonts => fonts.AddFontCollection(new EmbeddedFontCollection(
                      new Uri("fonts:Grimoire", UriKind.Absolute),
                      new Uri("avares://grimoire/Assets/Fonts", UriKind.Absolute))))
                  .With(new FontManagerOptions
                  {
                      DefaultFamilyName = "fonts:Grimoire#Kalam",
                      FontFallbacks = [new FontFallback { FontFamily = new FontFamily("fonts:Inter#Inter") }]
                  })
                  .LogToTrace();
}
