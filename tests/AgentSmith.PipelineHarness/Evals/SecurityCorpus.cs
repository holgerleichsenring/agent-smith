using System.Text.Json.Serialization;

namespace AgentSmith.PipelineHarness.Evals;

/// <summary>
/// 2026-08-28-cc40: a small repository whose every file carries a declared verdict — a
/// weakness with its class, or soundness shaped to look like one.
/// <para>
/// The files are INLINE rather than a checked-in tree, the trade
/// <see cref="AccountFixture"/> already settled for deliveries: a fixture is then one
/// reviewable file, small attributed excerpts carry no licence question, and a nested
/// repository inside this repository is a thing nobody wants to explain twice.
/// </para>
/// <para>
/// The corpus is a FLOOR and its report says so. Every public weakness corpus is in the
/// training data and a synthetic planting is formulaic in a way a real defect is not, so a
/// green score proves the scan is wired, reaches the code and emits findings at all — the
/// question this repository currently cannot answer — and proves nothing about quality.
/// </para>
/// </summary>
public sealed record SecurityCorpus
{
    [JsonPropertyName("id")] public string Id { get; init; } = string.Empty;

    [JsonPropertyName("description")] public string Description { get; init; } = string.Empty;

    [JsonPropertyName("files")]
    public IReadOnlyList<SecurityCorpusFile> Files { get; init; } = [];

    /// <summary>Files that hold a real weakness — the denominator of the miss rate.</summary>
    public IEnumerable<SecurityCorpusFile> Flawed => Files.Where(f => f.IsFlawed);

    /// <summary>Files that are sound and meant to look otherwise — the denominator of the
    /// false-alarm rate, and nothing else.</summary>
    public IEnumerable<SecurityCorpusFile> Clean => Files.Where(f => f.IsClean);

    /// <summary>Every file declares one of these, and an unrecognised word is a broken
    /// fixture rather than a third population.</summary>
    public static class Verdicts
    {
        public const string Flawed = "flawed";
        public const string Clean = "clean";

        public static IReadOnlyList<string> All => [Flawed, Clean];
    }
}

/// <summary>
/// One file of the corpus and the truth about it.
/// <para>
/// At most ONE weakness per file, deliberately. Scoring is per file, so two declarations in
/// one file would collapse into a single match and a scan that found one of them would
/// score as having found both.
/// </para>
/// </summary>
public sealed record SecurityCorpusFile
{
    /// <summary>Repository-relative path; also the match key a finding is scored against.</summary>
    [JsonPropertyName("path")] public string Path { get; init; } = string.Empty;

    [JsonPropertyName("content")] public string Content { get; init; } = string.Empty;

    /// <summary>One of <see cref="SecurityCorpus.Verdicts"/>.</summary>
    [JsonPropertyName("verdict")] public string Verdict { get; init; } = string.Empty;

    /// <summary>The weakness class for a flawed file; for a clean one, the class it is
    /// shaped to be mistaken for.</summary>
    [JsonPropertyName("class")] public string Class { get; init; } = string.Empty;

    /// <summary>Why the verdict is true, for the human reading a failing report.</summary>
    [JsonPropertyName("because")] public string Because { get; init; } = string.Empty;

    /// <summary>The line the weakness sits on, reported as a citation sub-metric and never
    /// as a gate: a finding that cites the call rather than the sink has still detected it.</summary>
    [JsonPropertyName("line")] public int Line { get; init; }

    public bool IsFlawed =>
        string.Equals(Verdict, SecurityCorpus.Verdicts.Flawed, StringComparison.Ordinal);

    public bool IsClean =>
        string.Equals(Verdict, SecurityCorpus.Verdicts.Clean, StringComparison.Ordinal);

    public bool HasKnownVerdict => IsFlawed || IsClean;
}
