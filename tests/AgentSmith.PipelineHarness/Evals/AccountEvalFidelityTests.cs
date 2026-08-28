using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Infrastructure.Services.Sandbox;
using AgentSmith.PipelineHarness.Llm;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.PipelineHarness.Evals;

/// <summary>
/// 2026-08-28-c310: the eval's input equals a run's input — the only property that makes a
/// score transferable off this harness and onto the component that ships.
/// <para>
/// Separate from <see cref="AccountEvalMechanicsTests"/> because that class proves the
/// arithmetic and this one proves the instrument. They fail for different reasons: a broken
/// rate is a scoring bug, a missing tool is a fidelity bug that leaves every rate intact and
/// wrong.
/// </para>
/// </summary>
public sealed class AccountEvalFidelityTests
{
    private static string CorpusDirectory =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "AccountDeliveries");

    private static AccountFixture Fixture(string id) =>
        AccountFixtureLoader.LoadAll(CorpusDirectory).Single(f => f.Id == id);

    /// <summary>The fixture that turns on the base: without <c>search_base</c> the account
    /// under test cannot answer it, and the corpus scores a component that never runs.</summary>
    [Fact]
    public async Task Harness_TheAccountUnderTest_IsOfferedTheBaseSearchTool()
    {
        var client = new PromptScriptedChatClient(SpecAccountReply.RefusingEverything);

        var entry = await ScoreAsync(
            Fixture("vacuous-conditional-topic-transport"), client);

        entry.Problem.Should().BeNull();
        client.OfferedTools.Should().BeEquivalentTo(["search_branch", "search_base"],
            "a run offers both wherever the delivery diff resolved a base");
        client.Prompts.Should().Contain(prompt => prompt.Contains(
            "The repositories with a base you can search are: Sample.Server",
            StringComparison.Ordinal));
    }

    /// <summary>The ref, not the sentence describing it: the search runs <c>git grep</c>
    /// against it, so it has to be the one the diff was actually taken against.</summary>
    [Fact]
    public async Task Harness_TheEvidence_CarriesTheBaseRefEachRepositoryWasDiffedAgainst()
    {
        await using var repositories = await AccountFixtureRepositories.MaterialiseAsync(
            Fixture("met-explicit-publish-routes"), NullLoggerFactory.Instance);

        var evidence = await DeliveryEvidence.GatherAsync(
            DeliveryDiffUnderTest(), repositories.Sandboxes, CancellationToken.None);

        evidence.Failures.Should().BeEmpty();
        evidence.BaseRefs.Should().BeEquivalentTo(new Dictionary<string, string?>
        {
            ["Sample.Server"] = "origin/main",
            ["Sample.Worker"] = "origin/main",
        });
        new BranchSearch(repositories.Sandboxes, NullLogger.Instance, evidence.BaseRefs)
            .BaseSearchable.Should().BeEquivalentTo(["Sample.Server", "Sample.Worker"]);
    }

    /// <summary>
    /// A clone with no <c>origin/HEAD</c> — a shallow one, in production — names no base, so
    /// the diff falls through to the branch itself and there is nothing to search. That is a
    /// delivery the eval must still score, offering only the tool a run would offer.
    /// </summary>
    [Fact]
    public async Task Harness_AFixtureWithNoBase_StillScoresWithoutThrowing()
    {
        var fixture = Fixture("absence-no-legacy-bus");
        await using var repositories = await AccountFixtureRepositories.MaterialiseAsync(
            fixture, NullLoggerFactory.Instance);
        ForgetTheRemote(repositories);
        var client = new PromptScriptedChatClient(SpecAccountReply.RefusingEverything);

        var entry = await HarnessOver(client).ScoreAsync(
            fixture, repositories, new AgentConfig(), CancellationToken.None);

        entry.Problem.Should().BeNull("a delivery with no base is a delivery, not a failure");
        entry.Criteria.Should().NotBeEmpty();
        client.OfferedTools.Should().BeEquivalentTo(["search_branch"],
            "a base tool that answers 'no base' for every call teaches the account to stop calling it");
    }

    /// <summary>The defect this phase fixes, asserted directly: refs resolved and the tool
    /// absent is the shape that lowered a score without failing anything.</summary>
    [Fact]
    public void AccountToolParity_ABaseThatResolvedButNoBaseTool_Throws()
    {
        var resolved = new Dictionary<string, string?> { ["Sample.Server"] = "origin/main" };
        var blind = new BranchSearch(
            new Dictionary<string, Contracts.Sandbox.ISandbox>(), NullLogger.Instance);

        var verify = () => AccountToolParity.Verify(resolved, blind);

        verify.Should().Throw<InvalidOperationException>().WithMessage("*search_base*");
    }

    [Fact]
    public void AccountToolParity_TheToolsARunOffers_AreAccepted()
    {
        var refs = new Dictionary<string, string?> { ["Sample.Server"] = null };
        var search = new BranchSearch(
            new Dictionary<string, Contracts.Sandbox.ISandbox>(), NullLogger.Instance, refs);

        var verify = () => AccountToolParity.Verify(refs, search);

        verify.Should().NotThrow();
    }

    /// <summary>Drops the remote-tracking refs the fixture builder writes, so the production
    /// base resolver gets the same answer a shallow clone gives it: nothing.</summary>
    private static void ForgetTheRemote(AccountFixtureRepositories repositories)
    {
        foreach (var sandbox in repositories.Sandboxes.Values.OfType<InProcessSandbox>())
        {
            var remotes = Path.Combine(sandbox.WorkDir, ".git", "refs", "remotes");
            if (Directory.Exists(remotes)) Directory.Delete(remotes, recursive: true);
        }
    }

    private static async Task<AccountEvalReport.FixtureEntry> ScoreAsync(
        AccountFixture fixture, PromptScriptedChatClient client)
    {
        await using var repositories = await AccountFixtureRepositories.MaterialiseAsync(
            fixture, NullLoggerFactory.Instance);
        return await HarnessOver(client).ScoreAsync(
            fixture, repositories, new AgentConfig(), CancellationToken.None);
    }

    private static AccountEvalHarness HarnessOver(PromptScriptedChatClient client)
    {
        var factory = new AccountEvalChatFactory(client, "scripted");
        return new AccountEvalHarness(
            new SpecAccountant(
                factory,
                new AccountCalls(new SpecAccountCall(
                    factory, new EvalRunContext(), NullLogger<SpecAccountCall>.Instance)),
                NullLogger<SpecAccountant>.Instance),
            NullLoggerFactory.Instance);
    }

    private static DeliveryDiff DeliveryDiffUnderTest() =>
        new(new SandboxBaseBranch(NullLogger<SandboxBaseBranch>.Instance),
            NullLogger<DeliveryDiff>.Instance);
}
