using FluentAssertions;
using Xunit.Abstractions;

namespace AgentSmith.PipelineHarness.Evals;

/// <summary>
/// 2026-08-28-cc40: the OPT-IN tier — the real security-scan preset, on a real agent CLI,
/// over a corpus whose flaws and traps are declared, scored in both directions.
/// <para>
/// Same gating as every paid suite here: Category=LiveLLM, excluded by CI, and a loud skip
/// with no CLI. It runs on a SUBSCRIPTION rather than a paid key — the worker bridge enters
/// as the model, so the whole machinery runs at no provider cost:
///   dotnet test tests/AgentSmith.PipelineHarness --filter "FullyQualifiedName~SecurityCorpusEvalTests"
/// </para>
/// <para>
/// The report lands in Reports/security-corpus/ named per model and per SCAN MASTER digest
/// — commit it. Its history is the baseline record, and the next change to the scan has to
/// show up there as a diff of the same file or it has not been measured.
/// </para>
/// </summary>
[Trait("Category", "LiveLLM")]
public sealed class SecurityCorpusEvalTests(ITestOutputHelper output)
{
    [Fact]
    public async Task EvalRun_OverTheSecurityCorpus_ScoresMissesAndFalseAlarms()
    {
        if (!AgentCliProbe.IsAvailable())
        {
            output.WriteLine(AgentCliProbe.SkipReason());
            return;
        }

        var corpora = SecurityCorpusLoader.LoadAll(SecurityCorpusLoader.DefaultDirectory);
        corpora.Should().NotBeEmpty("the corpus is what makes this a measurement");

        var report = await SecurityCorpusEvalHarness.RunAsync(corpora, CancellationToken.None);
        var mdPath = SecurityCorpusReportWriter.Write(report, ReportsDirectory());

        output.WriteLine($"Report: {mdPath}");
        output.WriteLine(
            $"Misses {report.Misses}/{report.FlawedPopulation} ({report.MissRate:P0}); "
            + $"false alarms {report.FalseAlarms}/{report.CleanPopulation} "
            + $"({report.FalseAlarmRate:P0}).");
        foreach (var entry in report.Entries.Where(e => e.Problem is not null))
            output.WriteLine($"  {entry.CorpusId}: SCAN NOT TAKEN — {entry.Problem}");
        foreach (var step in report.StepsThatContributedNothing)
            output.WriteLine($"  contributed nothing: {step}");

        report.Entries.Should().HaveCount(corpora.Count);
        report.Scored.Should().BeTrue(
            "a scan that could not be taken has no number: "
            + string.Join("; ", report.Entries.Select(e => e.Problem)));
        report.FlawedPopulation.Should().BeGreaterThan(0);
        report.CleanPopulation.Should().BeGreaterThan(0);
        File.Exists(Path.ChangeExtension(mdPath, ".json")).Should().BeTrue();

        // The floor: a scan that emits nothing over this corpus has not been measured to be
        // good or bad — it has been shown not to work, and it names what it walked past.
        report.Detections.Should().BeGreaterThan(0,
            "the scan found NONE of the declared weaknesses. Missed: "
            + string.Join(", ", report.MissedFiles)
            + ". Steps that contributed nothing: "
            + string.Join(", ", report.StepsThatContributedNothing));
    }

    // The committed report location: walk up from the test bin dir to the repo root, so a
    // re-run overwrites the version-controlled artifact rather than a bin-dir copy.
    private static string ReportsDirectory()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "AgentSmith.sln")))
            dir = dir.Parent;
        dir.Should().NotBeNull("the eval must run from a checkout to persist its report");
        return Path.Combine(dir!.FullName,
            "tests", "AgentSmith.PipelineHarness", "Reports", "security-corpus");
    }
}
