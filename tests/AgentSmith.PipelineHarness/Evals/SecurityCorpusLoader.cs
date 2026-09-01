using System.Text.Json;

namespace AgentSmith.PipelineHarness.Evals;

/// <summary>
/// 2026-08-28-cc40: loads the corpus and refuses a fixture carrying a customer fingerprint
/// — the same two-layer gate <see cref="AccountFixtureLoader"/> applies, for the same
/// reason: the material here is authored, so there is nothing to anonymise, and the day
/// someone pastes a real tree into it is the day that stops being true.
/// </summary>
public static class SecurityCorpusLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    /// <summary>The corpus directory as the built harness ships it.</summary>
    public static string DefaultDirectory =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "SecurityCorpus");

    public static IReadOnlyList<SecurityCorpus> LoadAll(string directory)
    {
        if (!Directory.Exists(directory)) return [];
        return
        [
            .. Directory.EnumerateFiles(directory, "*.json")
                .OrderBy(path => path, StringComparer.Ordinal)
                .Select(path => Load(path, directory)),
        ];
    }

    public static SecurityCorpus Load(string path, string? denyListDirectory)
    {
        var raw = File.ReadAllText(path);
        var violations = ExpectationFixtureAnonymizationCheck.CheckText(raw, denyListDirectory);
        if (violations.Count > 0)
            throw new InvalidOperationException(
                $"{Path.GetFileName(path)} carries customer fingerprints and will not load:\n  "
                + string.Join("\n  ", violations));
        var corpus = JsonSerializer.Deserialize<SecurityCorpus>(raw, Options)
            ?? throw new InvalidOperationException($"{Path.GetFileName(path)} is not a corpus.");
        Validate(corpus, Path.GetFileName(path));
        return corpus;
    }

    /// <summary>
    /// A fixture whose verdicts do not parse would score silently — every file would fall
    /// out of both denominators and the run would report two perfect rates over nothing.
    /// </summary>
    private static void Validate(SecurityCorpus corpus, string fileName)
    {
        var unknown = corpus.Files.Where(f => !f.HasKnownVerdict).Select(f => f.Path).ToList();
        if (unknown.Count > 0)
            throw new InvalidOperationException(
                $"{fileName} declares no verdict for: {string.Join(", ", unknown)}. "
                + $"Every file is one of {string.Join(" / ", SecurityCorpus.Verdicts.All)}.");
        var duplicates = corpus.Files.GroupBy(f => f.Path, StringComparer.Ordinal)
            .Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        if (duplicates.Count > 0)
            throw new InvalidOperationException(
                $"{fileName} declares the same path twice: {string.Join(", ", duplicates)}. "
                + "Scoring is per file, so one path is one verdict.");
    }
}
