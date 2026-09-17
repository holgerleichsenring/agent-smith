using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Models.Configuration;
using FluentAssertions;
using static AgentSmith.Tests.Specs.CutReviewTestDoubles;
using static AgentSmith.Tests.Specs.DerivationTestLooks;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// 2026-09-15-ffa7: the cut reviewer is handed a look of its own and told about it — which
/// repositories it may name, how many looks it has, how its ids are spelled. A look the
/// prompt does not describe cannot be used: every call is refused by name resolution.
/// </summary>
public sealed class CutReviewLookTests
{
    [Fact]
    public async Task Review_WithALook_PassesTheReadOnlyToolsAndTheIterationCeiling()
    {
        var provider = new LookingProvider("[]", "LegacyClient");
        var factory = new CappingFactory(provider);
        var sandbox = new CountingSandbox(exitCode: 1);
        var look = ReviewLook(sandbox);

        await Reviewer(factory).ReviewAsync(
            Drafts("every sender uses the new bus"), Key, "the ticket", look, new AgentConfig(), Tracker(), CancellationToken.None);

        factory.Caps.Should().Equal(SpecCutReviewer.MaxIterations);
        SpecCutReviewer.MaxIterations.Should().Be(DerivationLookTerms.CutReviewAllowance + 2);
        provider.ToolsOffered.Select(t => t.Name).Should().BeEquivalentTo(
            [RepositorySearchTool.Name, RepositoryFileReadTool.Name, DependencyAuditTool.Name]);
        sandbox.Ran.Should().ContainSingle("the look the reviewer asked for really ran");
    }

    [Fact]
    public async Task Review_Prompt_ListsTheRepositoriesTheLookCarries()
    {
        var prompt = await PromptOf(ReviewLook(new CountingSandbox(0)));

        prompt.Should().Contain("## Repositories you may look into").And.Contain($"- {Repo}");
    }

    [Fact]
    public async Task Review_Prompt_StatesTheReviewersAllowanceNotTheDerivations()
    {
        var prompt = await PromptOf(ReviewLook(new CountingSandbox(0)));

        prompt.Should().Contain($"up to {DerivationLookTerms.CutReviewAllowance} looks")
            .And.NotContain($"up to {DerivationLookTerms.DerivationAllowance} looks");
    }

    [Fact]
    public async Task Review_Prompt_StatesTheReviewersIdSpelling()
    {
        var prompt = await PromptOf(ReviewLook(new CountingSandbox(0)));

        prompt.Should().Contain("such as [R3]").And.NotContain("[L3]");
    }

    [Fact]
    public async Task Review_WithoutALook_StillReviewsTheTextAsBefore()
    {
        var provider = new LookingProvider("[]");
        var factory = new CappingFactory(provider);

        var review = await Reviewer(factory).ReviewAsync(
            Drafts("every sender uses the new bus"), Key, "the ticket", look: null, new AgentConfig(), Tracker(), CancellationToken.None);

        review.Deliverable.Should().BeTrue();
        factory.Caps.Should().Equal([null], "the call is the one it was: the factory default");
        provider.ToolsOffered.Should().BeEmpty();
        provider.Prompts.Single().Should().NotContain("Repositories you may look into")
            .And.NotContain(SpecCutVerdicts.FalsePremise);
    }

    [Fact]
    public void Tools_OnTheReviewersLook_DescribeItsOwnIdSpelling()
    {
        var look = ReviewLook(new CountingSandbox(0));

        look.Tools.Select(t => t.Description).Should().OnlyContain(d => d.Contains("such as [R"))
            .And.NotContain(d => d.Contains("[L"));
    }

    // 2026-09-17-042ed: the LLM layer's own NetworkTimeout arrives as a TaskCanceledException
    // while the RUN's token is untouched. Guarding on the exception's TYPE let it escape and take
    // the caller's whole turn with it — here the derivation's cut, and in a design turn the reply
    // the operator was about to be shown.
    [Fact]
    public async Task Review_WhoseModelCallTimesOut_IsNotDeliverableAndDoesNotThrow()
    {
        var review = await Reviewer(new CappingFactory(new ThrowingProvider(
            new TaskCanceledException("A task was canceled.")))).ReviewAsync(
            Drafts("every sender uses the new bus"), Key, "the ticket", look: null,
            new AgentConfig(), Tracker(), CancellationToken.None);

        review.Findings.Should().BeEmpty();
        review.Deliverable.Should().BeTrue("a review that could not be taken blocks nothing");
    }

    [Fact]
    public async Task Review_OnACancelledRun_Propagates()
    {
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        var reviewer = Reviewer(new CappingFactory(new ThrowingProvider(new OperationCanceledException())));

        var review = async () => await reviewer.ReviewAsync(
            Drafts("every sender uses the new bus"), Key, "the ticket", look: null,
            new AgentConfig(), Tracker(), cancelled.Token);

        await review.Should().ThrowAsync<OperationCanceledException>(
            "an operator cancel is the run ending, not a call to swallow");
    }

    private sealed class ThrowingProvider(Exception failure) : Microsoft.Extensions.AI.IChatClient
    {
        public Task<Microsoft.Extensions.AI.ChatResponse> GetResponseAsync(
            IEnumerable<Microsoft.Extensions.AI.ChatMessage> messages,
            Microsoft.Extensions.AI.ChatOptions? options = null, CancellationToken ct = default) =>
            throw failure;

        public IAsyncEnumerable<Microsoft.Extensions.AI.ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<Microsoft.Extensions.AI.ChatMessage> messages,
            Microsoft.Extensions.AI.ChatOptions? options = null,
            CancellationToken ct = default) => throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose() { }
    }

    private static async Task<string> PromptOf(DerivationLook look)
    {
        var provider = new LookingProvider("[]");
        await Reviewer(new CappingFactory(provider)).ReviewAsync(
            Drafts("every sender uses the new bus"), Key, "the ticket", look, new AgentConfig(), Tracker(), CancellationToken.None);
        return provider.Prompts.Single();
    }

    internal static DerivationLook ReviewLook(CountingSandbox sandbox) =>
        new(new Dictionary<string, Contracts.Sandbox.ISandbox> { [Repo] = sandbox },
            new FixedReaderFactory(new TestHelpers.InMemorySandboxFileReader()),
            new Application.Services.Sandbox.PackageEcosystemDetector(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance,
            templates: null, DerivationLookTerms.CutReview);
}
