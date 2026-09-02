using AgentSmith.Application.Services;
using AgentSmith.Contracts.Models;

namespace AgentSmith.PipelineHarness.Evals;

/// <summary>
/// 2026-08-28-cc40: matches delivered findings to declared files, per FILE.
/// <para>
/// A finding carries a file and a line; a fixture declares which FILE holds the weakness.
/// Scoring the line would fail a correct detection that cited the call rather than the
/// sink, so the line is carried as its own sub-metric and never as a gate.
/// </para>
/// <para>
/// Paths are compared with <see cref="CitedPathMatch.Same"/> — the merge already
/// owns the question of whether two paths name one file, prefix mismatch and all, and a
/// second normaliser here would be a second answer to it.
/// </para>
/// </summary>
public static class SecurityCorpusScoring
{
    public static IReadOnlyList<SecurityCorpusReport.FileOutcome> Score(
        SecurityCorpus corpus, IReadOnlyList<SkillObservation> findings)
    {
        ArgumentNullException.ThrowIfNull(corpus);
        ArgumentNullException.ThrowIfNull(findings);
        return [.. corpus.Files.Where(f => f.HasKnownVerdict).Select(file => Outcome(file, findings))];
    }

    private static SecurityCorpusReport.FileOutcome Outcome(
        SecurityCorpusFile file, IReadOnlyList<SkillObservation> findings)
    {
        var onFile = findings.Where(o => Names(o, file.Path)).ToList();
        return new SecurityCorpusReport.FileOutcome(
            file.Path,
            file.Class,
            file.IsFlawed,
            onFile.Count > 0,
            file.Line > 0 && onFile.Any(o => o.StartLine == file.Line),
            onFile.FirstOrDefault()?.Description,
            Loudest(onFile));
    }

    /// <summary>The severity a reader of the delivered set sees against this file — the
    /// highest one raised on it, because that is the one they act on.</summary>
    private static string? Loudest(IReadOnlyList<SkillObservation> onFile) =>
        onFile.Count == 0 ? null : onFile.Max(o => o.Severity).ToString();

    private static bool Names(SkillObservation observation, string declaredPath) =>
        !string.IsNullOrWhiteSpace(observation.File)
        && CitedPathMatch.Same(observation.File!, declaredPath);
}
