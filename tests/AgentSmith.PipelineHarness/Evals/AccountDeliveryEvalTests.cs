using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Models.Configuration;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit.Abstractions;

namespace AgentSmith.PipelineHarness.Evals;

/// <summary>
/// 2026-08-25-7035: the OPT-IN tier — the real delivery account, on a real model, over the
/// fixture deliveries, scored in both directions.
/// <para>
/// Same gating as every paid suite here: Category=LiveLLM, excluded by CI, and a loud skip
/// without credentials. Run it with:
///   OPENAI_API_KEY=sk-... dotnet test tests/AgentSmith.PipelineHarness --filter "FullyQualifiedName~AccountDeliveryEvalTests"
/// </para>
/// <para>
/// The report lands in Reports/account-deliveries/ named per model and per ACCOUNT PROMPT —
/// commit it. Its history is the baseline record, and the next change to the account has to
/// show up there as a diff or it has not been measured.
/// </para>
/// </summary>
[Trait("Category", "LiveLLM")]
public sealed class AccountDeliveryEvalTests(ITestOutputHelper output)
{
    [Fact]
    public async Task EvalRun_OverTheDeliveryCorpus_ScoresBothDirections()
    {
        var env = EvalChatClientEnv.TryBuild();
        if (env is null)
        {
            output.WriteLine("SKIP: no AZURE_OPENAI_API_KEY / OPENAI_API_KEY in env — "
                + "the account eval is paid-API and opt-in.");
            return;
        }

        var (client, modelId) = env.Value;
        var fixtures = AccountFixtureLoader.LoadAll(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "AccountDeliveries"));
        fixtures.Should().NotBeEmpty("the corpus is what makes this a measurement");

        var factory = new AccountEvalChatFactory(client, modelId);
        var accountant = new SpecAccountant(
            factory,
            new AccountCalls(new SpecAccountCall(factory, new EvalRunContext(), NullLogger<SpecAccountCall>.Instance)),
            NullLogger<SpecAccountant>.Instance);

        var report = await new AccountEvalHarness(accountant, NullLoggerFactory.Instance)
            .RunAsync(fixtures, new AgentConfig(), modelId, CancellationToken.None);

        var mdPath = AccountEvalReportWriter.Write(report, ReportsDirectory());
        output.WriteLine($"Report: {mdPath}");
        output.WriteLine(
            $"False negatives {report.FalseNegatives}/{report.MetPopulation} "
            + $"({report.FalseNegativeRate:P0}); false positives "
            + $"{report.FalsePositives}/{report.UnmetPopulation} ({report.FalsePositiveRate:P0}).");
        foreach (var entry in report.Entries.Where(e => e.Problem is not null))
            output.WriteLine($"  {entry.FixtureId}: ACCOUNT NOT TAKEN — {entry.Problem}");

        report.Entries.Should().HaveCount(fixtures.Count);
        report.MetPopulation.Should().BeGreaterThan(0);
        report.UnmetPopulation.Should().BeGreaterThan(0);
        File.Exists(mdPath).Should().BeTrue();
        File.Exists(Path.ChangeExtension(mdPath, ".json")).Should().BeTrue();
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
            "tests", "AgentSmith.PipelineHarness", "Reports", "account-deliveries");
    }
}
