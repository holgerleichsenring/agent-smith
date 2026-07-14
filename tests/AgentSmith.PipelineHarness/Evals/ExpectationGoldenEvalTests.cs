using AgentSmith.Application.Prompts;
using AgentSmith.Application.Services.Expectations;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Infrastructure.Core.Services.Skills;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit.Abstractions;

namespace AgentSmith.PipelineHarness.Evals;

/// <summary>
/// p0329: the OPT-IN eval tier — replays every golden through the real
/// drafting model and judges per assertion against the human gold. Same
/// gating as the LiveLLM probe: Category=LiveLLM (excluded by CI's
/// Category!=LiveLLM and by the PipelineHarness fast/docker filter) plus a
/// loud runtime skip without paid-API credentials. Run manually:
///   OPENAI_API_KEY=sk-... dotnet test --filter "Category=LiveLLM"
/// The report lands in tests/AgentSmith.PipelineHarness/Reports/
/// expectation-goldens/ named per model + skills pin — COMMIT it: that file's
/// history is the baseline record, and a skills/model change shows as a diff.
/// Baselines become meaningful once the operator ingests real historical
/// tickets (via ExpectationFixtureIngestion) next to the synthetic example.
/// </summary>
[Trait("Category", "LiveLLM")]
public sealed class ExpectationGoldenEvalTests(ITestOutputHelper output)
{
    [Fact]
    public async Task EvalRun_OverFixtureSet_WritesPerAssertionReport()
    {
        var env = EvalChatClientEnv.TryBuild();
        if (env is null)
        {
            output.WriteLine("SKIP: no AZURE_OPENAI_API_KEY / OPENAI_API_KEY in env — "
                + "the eval tier is paid-API and opt-in.");
            return;
        }

        var (client, modelId) = env.Value;
        var fixtures = ExpectationFixtureLoader.LoadAll(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "ExpectationGoldens"));
        var harness = new ExpectationEvalHarness(Drafter(client), new ExpectationEvalJudge(client));

        var report = await harness.RunAsync(
            fixtures, new AgentConfig(), modelId,
            new EmbeddedSkillsCatalog().Version, CancellationToken.None);

        var mdPath = ExpectationEvalReportWriter.Write(report, ReportsDirectory());
        output.WriteLine($"Report: {mdPath}");
        output.WriteLine($"Matched {report.Matched}/{report.TotalGold} "
            + $"({report.MatchedRate:P0}), hallucinated {report.Hallucinated}.");
        report.Entries.Should().HaveCount(fixtures.Count);
        File.Exists(mdPath).Should().BeTrue();
        File.Exists(Path.ChangeExtension(mdPath, ".json")).Should().BeTrue();
    }

    private static ExpectationDrafter Drafter(Microsoft.Extensions.AI.IChatClient client) => new(
        new SingleClientChatFactory(client, "eval"),
        new EmbeddedPromptCatalog(
            new EnvDirectoryPromptOverrideSource(NullLogger<EnvDirectoryPromptOverrideSource>.Instance),
            NullLogger<EmbeddedPromptCatalog>.Instance),
        new ExpectationDraftValidator(),
        new EvalRunContext(),
        NullLogger<ExpectationDrafter>.Instance);

    // The committed report location: walk up from the test bin dir to the repo
    // root (the directory holding AgentSmith.sln) so re-runs overwrite the
    // version-controlled artifact, not a bin-dir copy.
    private static string ReportsDirectory()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "AgentSmith.sln")))
            dir = dir.Parent;
        dir.Should().NotBeNull("the eval must run from a checkout to persist its report");
        return Path.Combine(dir!.FullName,
            "tests", "AgentSmith.PipelineHarness", "Reports", "expectation-goldens");
    }
}
