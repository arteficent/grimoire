using System.Text.Encodings.Web;
using System.Text.Json;
using grimoire.Stories;

namespace grimoire.Tests;

/// <summary>Keeps docs/catalog.js (read by the GitHub Pages site) in sync with the lessons in the app.</summary>
public sealed class CatalogTests
{
    private const string UpdateVariable = "GRIMOIRE_UPDATE_CATALOG";

    [Fact]
    public void Docs_catalog_matches_the_curriculum()
    {
        var path = Path.Combine(RepoPaths.Root, "docs", "catalog.js");
        var expected = Render();

        if (Environment.GetEnvironmentVariable(UpdateVariable) == "1")
        {
            File.WriteAllText(path, expected);
        }

        Assert.True(File.Exists(path), $"{path} is missing; run the tests with {UpdateVariable}=1 to create it.");
        var actual = File.ReadAllText(path).ReplaceLineEndings("\n");
        Assert.True(actual == expected, $"docs/catalog.js is out of date; run the tests with {UpdateVariable}=1 and commit the result.");
    }

    private static string Render()
    {
        var catalog = new
        {
            lessonCount = Curriculum.Tracks.Sum(t => t.Lessons.Count),
            stepCount = Curriculum.Tracks.Sum(t => t.Lessons.Sum(l => l.Story.Steps.Count)),
            tracks = Curriculum.Tracks.Select(t => new
            {
                id = t.Id,
                title = t.Title,
                tagline = t.Tagline,
                accent = t.Accent,
                monogram = t.Monogram,
                lessons = t.Lessons.Select(l => new
                {
                    id = l.Id,
                    title = l.Title,
                    section = l.Section,
                    summary = l.Summary,
                    steps = l.Story.Steps.Count,
                    runnable = l.Live is not null
                })
            })
        };

        var json = JsonSerializer.Serialize(catalog, new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        }).ReplaceLineEndings("\n");

        return $"// Generated from the app's curriculum by tests/grimoire.Tests. Do not edit by hand:\n// set {UpdateVariable}=1 and run `dotnet test` to regenerate.\nwindow.GRIMOIRE_CATALOG = {json};\n";
    }
}
