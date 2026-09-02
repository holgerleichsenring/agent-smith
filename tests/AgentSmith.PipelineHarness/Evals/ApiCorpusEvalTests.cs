using FluentAssertions;
using Xunit.Abstractions;

namespace AgentSmith.PipelineHarness.Evals;

/// <summary>
/// 2026-09-01-6686: the OPT-IN tier — the real api-security-scan preset, on a real agent
/// CLI, against a target this repository serves itself.
/// <para>
/// No external machine and no docker: the target is a loopback server this test starts,
/// and the dynamic scanners stay stubbed unless an operator opts in — which the score
/// NAMES, so a run where half the scan never executed is never read as a detection result.
/// </para>
/// <para>
/// Report: Reports/api-corpus/, named per model and per api-security-master digest —
/// commit it. A change to the api scan has to show up there as a diff of the same file or
/// it has not been measured.
/// </para>
/// </summary>
[Trait("Category", "LiveLLM")]
public sealed class ApiCorpusEvalTests(ITestOutputHelper output)
{
    [Fact]
    public async Task EvalRun_AgainstTheServedTarget_ScoresMissesAndFalseAlarms()
    {
        if (!AgentCliProbe.IsAvailable())
        {
            output.WriteLine(AgentCliProbe.SkipReason());
            return;
        }

        var declaration = ApiTargetDeclarationLoader.Load(ApiTargetDeclarationLoader.DefaultPath);

        var report = await ApiCorpusEvalHarness.RunAsync(declaration, CancellationToken.None);
        var mdPath = ApiCorpusReportWriter.Write(report, ReportsDirectory());

        output.WriteLine($"Report: {mdPath}");
        output.WriteLine(
            $"Misses {report.Misses}/{report.WeakPopulation} ({report.MissRate:P0}); "
            + $"false alarms {report.FalseAlarms}/{report.SoundPopulation} "
            + $"({report.FalseAlarmRate:P0}).");
        foreach (var step in report.StepsThatContributedNothing)
            output.WriteLine($"  contributed nothing: {step}");
        foreach (var location in report.UndeclaredLocations)
            output.WriteLine($"  named nothing declared: {location}");

        report.Scored.Should().BeTrue($"a scan that could not be taken has no number: {report.Problem}");
        report.WeakPopulation.Should().BeGreaterThan(0);
        report.SoundPopulation.Should().BeGreaterThan(0);
        File.Exists(Path.ChangeExtension(mdPath, ".json")).Should().BeTrue();

        report.Detections.Should().BeGreaterThan(0,
            "the scan found NONE of the declared weaknesses. Missed: "
            + string.Join(", ", report.MissedEndpoints)
            + ". Steps that contributed nothing: "
            + string.Join(", ", report.StepsThatContributedNothing));
    }

    private static string ReportsDirectory()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "AgentSmith.sln")))
            dir = dir.Parent;
        dir.Should().NotBeNull("the eval must run from a checkout to persist its report");
        return Path.Combine(dir!.FullName,
            "tests", "AgentSmith.PipelineHarness", "Reports", "api-corpus");
    }
}
