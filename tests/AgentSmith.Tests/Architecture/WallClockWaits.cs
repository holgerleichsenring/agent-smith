using System.Text.RegularExpressions;

namespace AgentSmith.Tests.Architecture;

/// <summary>
/// 2026-09-22-3f7c: the shapes by which a test's outcome comes to depend on how fast the
/// machine is — a bounded wait, a delay followed by a check, a deadline held against a
/// clock. Found by SHAPE rather than by which tests have failed so far, because the ones
/// that have failed are the symptom and not the population.
/// </summary>
internal static class WallClockWaits
{
    private static readonly Regex[] Shapes =
    [
        // A delay measured in time. Task.Delay(Timeout.Infinite, ct) is a park, not a wait.
        new(@"Task\.Delay\(\s*(?!Timeout\.Infinite)", RegexOptions.Compiled),
        new(@"Thread\.Sleep\(", RegexOptions.Compiled),
        // A wait given a deadline — as opposed to one given a cancellation token.
        new(@"\.WaitAsync\(\s*(TimeSpan|\d)", RegexOptions.Compiled),
        new(@"\.Wait\(\s*(TimeSpan|\d)", RegexOptions.Compiled),
        new(@"CancelAfter\(", RegexOptions.Compiled),
        new(@"new CancellationTokenSource\(\s*[^)\s]", RegexOptions.Compiled),
        // An assertion whose SUBJECT is a MEASURED duration. A configured TimeSpan held
        // against a bound (a question's own answer window, a backoff the code computed) is
        // arithmetic, not a stopwatch, and no machine changes its answer.
        new(@"(Duration|Elapsed|Waited)[A-Za-z]*\.Should\(\)\.Be(Less|Greater)", RegexOptions.Compiled),
    ];

    /// <summary>The suite's wall-clock vocabulary itself — the one place a ceiling lives.</summary>
    private const string Vocabulary = "TestWaits.cs";

    /// <summary>The category the gate and the pull-request workflow already exclude.</summary>
    private const string ExcludedCategory = "Trait(\"Category\", \"LiveLLM\")";

    /// <summary>Path (relative to <c>tests/</c>) to how many wall-clock shapes it carries.</summary>
    public static IReadOnlyDictionary<string, int> Counted()
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var root in ArchitectureSources.GatedTestRoots)
            foreach (var file in Files(root))
            {
                var text = File.ReadAllText(file);
                if (text.Contains(ExcludedCategory, StringComparison.Ordinal)) continue;
                var found = File.ReadLines(file).Count(line => Shapes.Any(s => s.IsMatch(line)));
                if (found > 0) counts[Relative(file)] = found;
            }
        return counts;
    }

    private static IEnumerable<string> Files(string root) =>
        Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                        && !p.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(p => Path.GetFileName(p) != Vocabulary);

    private static string Relative(string file) =>
        Path.GetRelativePath(Path.Combine(ArchitectureSources.RepositoryRoot, "tests"), file)
            .Replace('\\', '/');
}
