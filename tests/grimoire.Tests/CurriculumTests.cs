using System.Text.RegularExpressions;
using grimoire.Model;
using grimoire.Stories;

namespace grimoire.Tests;

public sealed class CurriculumTests
{
    private static readonly IReadOnlyList<Track> Tracks = Curriculum.Tracks;

    private static IEnumerable<(Track Track, Lesson Lesson)> AllLessons =>
        Tracks.SelectMany(t => t.Lessons.Select(l => (t, l)));

    private static IEnumerable<(Lesson Lesson, int Index, StoryStep Step)> AllSteps =>
        AllLessons.SelectMany(x => x.Lesson.Story.Steps.Select((s, i) => (x.Lesson, i + 1, s)));

    [Fact]
    public void Every_track_has_lessons_and_branding()
    {
        Assert.NotEmpty(Tracks);
        foreach (var track in Tracks)
        {
            Assert.False(string.IsNullOrWhiteSpace(track.Title), $"track {track.Id} has no title");
            Assert.Matches("^#[0-9A-Fa-f]{6}$", track.Accent);
            Assert.NotEmpty(track.Lessons);
        }
    }

    [Fact]
    public void Track_and_lesson_ids_are_unique()
    {
        Assert.Equal(Tracks.Count, Tracks.Select(t => t.Id).Distinct().Count());

        var duplicates = AllLessons.GroupBy(x => x.Lesson.Id).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        Assert.True(duplicates.Count == 0, "duplicate lesson ids: " + string.Join(", ", duplicates));
    }

    [Fact]
    public void Every_lesson_has_titled_steps_with_narrative()
    {
        foreach (var (track, lesson) in AllLessons)
        {
            var where = $"{track.Id}/{lesson.Id}";
            Assert.False(string.IsNullOrWhiteSpace(lesson.Title), $"{where} has no title");
            Assert.False(string.IsNullOrWhiteSpace(lesson.Section), $"{where} has no section");
            Assert.False(string.IsNullOrWhiteSpace(lesson.Story.Takeaway), $"{where} has no takeaway");
            Assert.NotEmpty(lesson.Story.Steps);
            foreach (var step in lesson.Story.Steps)
            {
                Assert.False(string.IsNullOrWhiteSpace(step.Title), $"{where} has an untitled step");
                Assert.False(string.IsNullOrWhiteSpace(step.Narrative), $"{where} step '{step.Title}' has no narrative");
            }
        }
    }

    [Fact]
    public void Highlighted_code_lines_exist()
    {
        var problems = new List<string>();
        foreach (var (lesson, index, step) in AllSteps)
        {
            var count = string.IsNullOrEmpty(step.Code) ? 0 : step.Code.Split('\n').Length;
            problems.AddRange(step.Lines.Where(line => line < 1 || line > count)
                .Select(line => $"{lesson.Id} step {index} highlights line {line}, code has {count} lines"));
        }

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    [Fact]
    public void Links_and_moves_point_at_existing_shapes()
    {
        var problems = new List<string>();
        foreach (var (lesson, index, step) in AllSteps)
        {
            var ids = step.Scene.Shapes.Select(s => s.Id).ToHashSet();
            if (ids.Count == 0)
            {
                continue;
            }

            foreach (var link in step.Scene.Links)
            {
                if (!ids.Contains(link.From) || !ids.Contains(link.To))
                {
                    problems.Add($"{lesson.Id} step {index}: link {link.From} -> {link.To} references a missing shape");
                }
            }

            foreach (var move in step.Scene.Transfers)
            {
                foreach (var end in new[] { move.From, move.To }.Where(e => e.StartsWith("shape:", StringComparison.Ordinal)))
                {
                    if (!ids.Contains(end["shape:".Length..]))
                    {
                        problems.Add($"{lesson.Id} step {index}: move to {end} references a missing shape");
                    }
                }
            }
        }

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    [Fact]
    public void Shape_ids_are_unique_within_a_step()
    {
        var problems = AllSteps
            .SelectMany(x => x.Step.Scene.Shapes.GroupBy(s => s.Id).Where(g => g.Count() > 1).Select(g => $"{x.Lesson.Id} step {x.Index}: shape '{g.Key}' appears {g.Count()} times"))
            .ToList();

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    [Fact]
    public void App_version_is_semantic_and_matches_the_build_props()
    {
        Assert.Matches(@"^\d+\.\d+\.\d+$", AppInfo.Version);

        var props = File.ReadAllText(Path.Combine(RepoPaths.Root, "Directory.Build.props"));
        var prefix = Regex.Match(props, "<VersionPrefix>([^<]+)</VersionPrefix>").Groups[1].Value;
        Assert.Equal(prefix, AppInfo.Version);
    }
}
