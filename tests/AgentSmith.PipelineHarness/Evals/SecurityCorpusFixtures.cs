using AgentSmith.Contracts.Models;

namespace AgentSmith.PipelineHarness.Evals;

/// <summary>
/// 2026-08-28-cc40: the small corpora and findings the mechanics cases score, so each case
/// states the ONE thing it is about and nothing else. Built in code rather than read from
/// disk: a case about arithmetic must not fail because the committed corpus grew a file.
/// </summary>
internal static class SecurityCorpusFixtures
{
    internal const string FlawedPath = "src/orders/orderLookup.ts";
    internal const string CleanPath = "src/reports/reportLookup.ts";
    internal const int FlawedLine = 7;

    /// <summary>One flawed file and one clean trap — the smallest corpus with both
    /// denominators.</summary>
    internal static SecurityCorpus OneOfEach() => new()
    {
        Id = "mechanics",
        Files =
        [
            new SecurityCorpusFile
            {
                Path = FlawedPath, Verdict = SecurityCorpus.Verdicts.Flawed,
                Class = "sql-injection", Line = FlawedLine, Content = "// flawed",
            },
            new SecurityCorpusFile
            {
                Path = CleanPath, Verdict = SecurityCorpus.Verdicts.Clean,
                Class = "sql-injection", Line = 15, Content = "// sound",
            },
        ],
    };

    /// <summary>A finding as the delivery layer carries one: role, file, line, headline.</summary>
    internal static SkillObservation FindingOn(string file, int line) => new(
        Id: 1, Role: "security-master", Concern: ObservationConcern.Security,
        Description: $"weakness at {file}:{line}", Suggestion: "fix it", Blocking: false,
        Severity: ObservationSeverity.High, Confidence: 80,
        File: file, StartLine: line, EvidenceMode: EvidenceMode.AnalyzedFromSource);

    internal static SecurityCorpusReport ReportOf(
        SecurityCorpus corpus, params SkillObservation[] findings) =>
        new("test-model", "0000abcd", DateTimeOffset.UnixEpoch,
        [
            new SecurityCorpusReport.CorpusEntry(
                corpus.Id, SecurityCorpusScoring.Score(corpus, findings), [], null),
        ]);
}
