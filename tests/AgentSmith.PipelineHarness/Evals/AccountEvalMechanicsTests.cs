using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Models;
using AgentSmith.PipelineHarness.Llm;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.PipelineHarness.Evals;

/// <summary>
/// 2026-08-25-7035: the model-free half — fixture materialisation, the search tool against a
/// real tree, the scoring arithmetic and the report shape, all provable without a paid call.
/// A number that cannot be recomputed without credentials is a number nobody recomputes.
/// </summary>
public sealed class AccountEvalMechanicsTests
{
    private static string CorpusDirectory =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "AccountDeliveries");

    private static AccountFixture Fixture(string id) =>
        AccountFixtureLoader.LoadAll(CorpusDirectory).Single(f => f.Id == id);

    [Fact]
    public void AccountFixture_TheCorpus_CoversEveryCriterionClassItClaims()
    {
        var classes = AccountFixtureLoader.LoadAll(CorpusDirectory)
            .Select(f => f.Class).Distinct(StringComparer.Ordinal).ToList();

        classes.Should().BeEquivalentTo(AccountFixture.CriterionClasses.All,
            "a class that loses its only fixture stops being measured without anything failing");
    }

    [Fact]
    public void AccountFixture_TheCorpus_ScoresInBothDirections()
    {
        var criteria = AccountFixtureLoader.LoadAll(CorpusDirectory)
            .SelectMany(f => f.Criteria).ToList();

        criteria.Should().Contain(c => c.IsMet, "the false-negative rate needs a denominator");
        criteria.Should().Contain(c => !c.IsMet, "the false-positive rate needs one too");
        criteria.Should().OnlyContain(c => c.HasKnownTruth && c.Because.Length > 0);
    }

    [Fact]
    public async Task AccountFixture_Materialised_IsARealRepositoryWithABaseAndABranch()
    {
        await using var repos = await AccountFixtureRepositories.MaterialiseAsync(
            Fixture("met-explicit-publish-routes"), NullLoggerFactory.Instance);

        repos.Sandboxes.Should().ContainKeys("Sample.Server", "Sample.Worker");
        var diff = await DiffOf(repos, "Sample.Server");
        diff.Should().Contain("DisableConventionalLocalRouting")
            .And.Contain("--- a/src/Messaging/Installer.cs",
                "the diff is taken against the base ref the clone itself names");
    }

    /// <summary>The point of a real tree: the tool answers a pattern the fixture never
    /// declared, which is what a changed prompt will ask.</summary>
    [Fact]
    public async Task AccountFixture_ItsSearchTool_AnswersAPatternTheFixtureNeverDeclared()
    {
        await using var repos = await AccountFixtureRepositories.MaterialiseAsync(
            Fixture("absence-no-legacy-bus"), NullLoggerFactory.Instance);
        var search = new BranchSearch(repos.Sandboxes, NullLogger.Instance);

        var gone = await search.SearchBranch("Sample.Server", "LegacyBus");
        var present = await search.SearchBranch("Sample.Server", "IMessageSender");

        gone.Should().Contain("does not occur anywhere");
        present.Should().Contain("OrderService.cs");
    }

    [Fact]
    public async Task AccountFixture_ADeclaredWindowBudget_ActuallySplitsTheDelivery()
    {
        var fixture = Fixture("universal-across-windows");
        await using var repos = await AccountFixtureRepositories.MaterialiseAsync(
            fixture, NullLoggerFactory.Instance);

        var combined = string.Join("\n", await Task.WhenAll(
            repos.Sandboxes.Keys.Select(async key => await DiffOf(repos, key))));

        DiffWindows.Split(combined, fixture.WindowBudgetChars!.Value).Count
            .Should().BeGreaterThan(1,
                "the fixture exists to reproduce a split; a budget that does not split it "
                + "measures nothing the production number does not already measure");
    }

    /// <summary>
    /// 2026-08-25-0eae, end to end on a real repository: the ref the delivery diff settled on
    /// is a ref the account can read, and it answers a question the branch cannot. The base
    /// carries the legacy bus; the branch does not. No mock can prove this — the point is that
    /// git resolves the ref the production ladder picked.
    /// </summary>
    [Fact]
    public async Task AccountFixture_TheResolvedBaseRef_IsSearchableAndAnswersWhatTheBranchCannot()
    {
        var fixture = Fixture("absence-no-legacy-bus");
        await using var repos = await AccountFixtureRepositories.MaterialiseAsync(
            fixture, NullLoggerFactory.Instance);

        var deliveryDiff = new AgentSmith.Application.Services.DeliveryDiff(
            new AgentSmith.Application.Services.Sandbox.SandboxBaseBranch(
                NullLogger<AgentSmith.Application.Services.Sandbox.SandboxBaseBranch>.Instance),
            new AgentSmith.Application.Services.Sandbox.SandboxRunStartCommit(
                NullLogger<AgentSmith.Application.Services.Sandbox.SandboxRunStartCommit>.Instance),
            NullLogger<AgentSmith.Application.Services.DeliveryDiff>.Instance);
        var diff = await deliveryDiff.ForBranchAsync(
            repos.Sandboxes["Sample.Server"], runId: null, CancellationToken.None);

        diff.BaseRef.Should().Be("origin/main", "the fixture names its base the way a clone does");

        var search = new BranchSearch(
            repos.Sandboxes, NullLogger.Instance,
            new Dictionary<string, string?> { ["Sample.Server"] = diff.BaseRef });

        search.BaseSearchable.Should().ContainSingle();
        (await search.SearchBase("Sample.Server", "LegacyBus"))
            .Should().Contain("found in", "the base is where the legacy bus still lives");
        (await search.SearchBranch("Sample.Server", "LegacyBus"))
            .Should().Contain("does not occur anywhere");
        search.Evidence.Should().HaveCount(2)
            .And.Contain(e => e.StartsWith("Sample.Server@origin/main:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AccountEval_ARefusalOfAMetCriterion_ScoresAFalseNegative()
    {
        var report = await ScoreAsync("met-explicit-publish-routes", satisfyEverything: false);

        report.MetPopulation.Should().Be(1);
        report.FalseNegatives.Should().Be(1);
        report.FalseNegativeRate.Should().Be(1d);
        report.FalsePositives.Should().Be(0, "refusing an unmet criterion is the right answer");
    }

    [Fact]
    public async Task AccountEval_APassOfAnUnmetCriterion_ScoresAFalsePositive()
    {
        var report = await ScoreAsync("met-explicit-publish-routes", satisfyEverything: true);

        report.UnmetPopulation.Should().Be(1);
        report.FalsePositives.Should().Be(1);
        report.FalsePositiveRate.Should().Be(1d);
        report.FalseNegatives.Should().Be(0, "passing a met criterion is the right answer");
    }

    [Fact]
    public async Task AccountEval_EachRate_IsComputedOverItsOwnDenominator()
    {
        var report = await ScoreAsync("met-explicit-publish-routes", satisfyEverything: true);

        (report.MetPopulation + report.UnmetPopulation).Should().Be(2);
        report.MetPopulation.Should().NotBe(report.MetPopulation + report.UnmetPopulation,
            "a rate over the whole population is the arithmetic that hid the problem");
    }

    [Fact]
    public async Task AccountEval_AnAccountThatSaidNothing_IsNotScoredAsAgreement()
    {
        var report = await ScoreAsync("met-explicit-publish-routes", answer: "[]");

        report.Entries.Single().Criteria.Should().OnlyContain(c => c.Blocks);
        report.FalseNegatives.Should().Be(1, "silence is what the gate acts on");
    }

    [Fact]
    public async Task AccountEval_TheReport_StatesBothPopulationsAndIsWritten()
    {
        var report = await ScoreAsync("met-explicit-publish-routes", satisfyEverything: true);
        var directory = Path.Combine(Path.GetTempPath(), "account-eval-report-" + Guid.NewGuid().ToString("n"));

        var path = AccountEvalReportWriter.Write(report, directory);
        var markdown = File.ReadAllText(path);

        markdown.Should().Contain("False negatives:").And.Contain("False positives:")
            .And.Contain("/1").And.Contain("classes covered:");
        File.Exists(Path.ChangeExtension(path, ".json")).Should().BeTrue();
        Directory.Delete(directory, recursive: true);
    }

    [Fact]
    public void AccountEval_TheReportName_MovesWithTheAccountPrompt()
    {
        AccountPromptVersion.Current.Should().MatchRegex("^[0-9a-f]{8}$");
        AccountPromptVersion.Current.Should().Be(AccountPromptVersion.Current);
    }

    private static async Task<AccountEvalReport> ScoreAsync(
        string fixtureId, bool satisfyEverything = true, string? answer = null)
    {
        var client = new PromptScriptedChatClient(prompt => answer ?? (satisfyEverything
            ? SpecAccountReply.SatisfyingEverything(prompt)
            : SpecAccountReply.RefusingEverything(prompt)));
        var factory = new AccountEvalChatFactory(client, "scripted");
        var accountant = new SpecAccountant(
            factory,
            new AccountCalls(new SpecAccountCall(factory, new EvalRunContext(), NullLogger<SpecAccountCall>.Instance)),
            NullLogger<SpecAccountant>.Instance);

        return await new AccountEvalHarness(accountant, NullLoggerFactory.Instance).RunAsync(
            [Fixture(fixtureId)], new AgentConfig(), "scripted", CancellationToken.None);
    }

    private static async Task<string> DiffOf(AccountFixtureRepositories repos, string key)
    {
        var deliveryDiff = new AgentSmith.Application.Services.DeliveryDiff(
            new AgentSmith.Application.Services.Sandbox.SandboxBaseBranch(
                NullLogger<AgentSmith.Application.Services.Sandbox.SandboxBaseBranch>.Instance),
            new AgentSmith.Application.Services.Sandbox.SandboxRunStartCommit(
                NullLogger<AgentSmith.Application.Services.Sandbox.SandboxRunStartCommit>.Instance),
            NullLogger<AgentSmith.Application.Services.DeliveryDiff>.Instance);
        var result = await deliveryDiff.ForBranchAsync(
            repos.Sandboxes[key], runId: null, CancellationToken.None);
        result.Failed.Should().BeFalse("a fixture whose diff cannot be taken measures nothing");
        return result.Text;
    }
}
