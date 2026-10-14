using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Events;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Loop;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Application.Services.Specs.Review;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Tests.Specs;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using static AgentSmith.Tests.SpecDialog.ProposalReview.TicketPlanReviewRig;

namespace AgentSmith.Tests.SpecDialog.ProposalReview;

/// <summary>
/// 2026-10-14-dbcek: what the plan reviewer keeps of its model's answer, and what it is shown — the operator's
/// messages within a window, the uploads by their reference: address, readable through its look.
/// </summary>
public sealed class TicketPlanReviewerTests
{
    private static TicketGraphOutcome Two() => List(Ticket("order-api"), Ticket("order-ui", "order-api"));

    [Fact]
    public async Task TicketPlanReviewer_FindingForUnknownTicket_Dropped()
    {
        var verdict = await ReviewAsync(Finding("order-export", "merge", quote: "in prose", replacement: ""));

        verdict!.Findings.Should().BeEmpty();
        verdict.Cuts.Should().BeEmpty();
    }

    [Fact]
    public async Task TicketPlanReviewer_QuoteNotInTheTicket_FindingKeptWithoutCut()
    {
        var verdict = await ReviewAsync(Finding("order-ui", "size", quote: "and the order edit form", replacement: ""));

        verdict!.Findings.Should().ContainSingle().Which.SpecId.Should().Be("order-ui");
        verdict.Cuts.Should().BeEmpty();
    }

    [Fact]
    public async Task TicketPlanReviewer_QuoteInTheTicket_IsACut()
    {
        var verdict = await ReviewAsync(Finding("order-ui", "size", quote: "in prose", replacement: ""));

        verdict!.Cuts.Should().ContainSingle().Which.Should().Be(new TicketPlanCut("order-ui", "in prose", "", "size on order-ui"));
    }

    [Fact]
    public async Task TicketPlanReviewer_MissingRequirement_FindingOnly()
    {
        var verdict = await ReviewAsync(Finding("order-ui", TicketPlanReviewPrompt.Missing, quote: "in prose", replacement: ""));

        verdict!.Findings.Should().ContainSingle().Which.SpecId.Should().Be(TicketPlanReviewPrompt.ListKey);
        verdict.Cuts.Should().BeEmpty("nothing in the list can be quoted for what is absent");
    }

    [Fact]
    public async Task TicketPlanReviewer_MissingRequirementNamingNoTicket_KeptAsListFinding()
    {
        var verdict = await ReviewAsync(Finding(TicketPlanReviewPrompt.ListKey, TicketPlanReviewPrompt.Missing, quote: "refunds of cancelled orders"));

        var finding = verdict!.Findings.Should().ContainSingle().Subject;
        finding.SpecId.Should().Be("the list");
        finding.Quote.Should().Be("refunds of cancelled orders");
    }

    [Fact]
    public async Task TicketPlanReviewer_UploadedRequirements_ReadableThroughTheLook()
    {
        const string upload = "reference:requirements";
        var (repository, reference) = (new DerivationTestLooks.CountingSandbox(0), new DerivationTestLooks.CountingSandbox(0, "refunds"));
        var provider = new SearchingProvider(upload);
        var rig = new TicketPlanReviewRig();
        rig.Pipeline.Set(ContextKeys.Sandboxes, (IReadOnlyDictionary<string, ISandbox>)new Dictionary<string, ISandbox>
        {
            ["shop"] = repository, [upload] = reference, ["template:house-style"] = new DerivationTestLooks.CountingSandbox(0),
        });
        var reviewer = new TicketPlanReviewer(new CutReviewTestDoubles.CappingFactory(provider), new AsyncLocalRunContextAccessor(),
            DerivationTestLooks.Factory(), NullLogger<TicketPlanReviewer>.Instance);

        var verdict = await reviewer.ReviewAsync(Turn(rig.Pipeline), Two(), CancellationToken.None);

        verdict.Should().NotBeNull();
        reference.Ran.Should().ContainSingle("the uploaded material is one of the look's names");
        provider.Prompt.Should().Contain($"- {upload}").And.Contain("- shop").And.NotContain("template:house-style");
    }

    [Fact]
    public void TicketPlanReviewPrompt_For_NamesTheUploadsAndSaysExistingAndLaterRequirementsAreNotMissing()
    {
        var prompt = TicketPlanReviewPrompt.For(Two(), [new SpecDialogTurn(SpecDialogTurn.UserRole, "Orders need refunds.")], null,
            ["reference:requirements"]);

        prompt.Should().Contain("- reference:requirements").And.Contain("Orders need refunds.")
            .And.Contain("A requirement met by a ticket:<id> requirement, or one the conversation says is left for a later list, is not missing.")
            .And.Contain("key: order-ui").And.Contain("requires: order-api");
    }

    [Fact]
    public void TicketPlanReviewPrompt_RequirementsOverTheWindow_KeepsFirstTurnAndNamesTheOmitted()
    {
        var big = new string('x', 15_000);
        IReadOnlyList<SpecDialogTurn> transcript =
        [
            User("FIRST " + big), new SpecDialogTurn(SpecDialogTurn.AssistantRole, "noted"), User("SECOND " + big),
            User("THIRD " + big), User("FOURTH " + big),
        ];

        var requirements = TicketPlanReviewPrompt.Requirements(transcript);

        requirements.Should().StartWith("FIRST ").And.Contain("(2 message(s) between these were left out to fit the window)")
            .And.Contain("FOURTH ").And.NotContain("SECOND ").And.NotContain("noted");
        requirements.Length.Should().BeLessThan(TicketPlanReviewPrompt.RequirementWindowChars + 200);
    }

    [Fact]
    public void TicketPlanReviewPrompt_CutsNudge_ListsQuoteAndReplacementPerKeyAndKeepsKeys()
    {
        var nudge = TicketPlanReviewPrompt.CutsNudge(
            [new TicketPlanCut("order-ui", "and the edit form", "", "too big"), new TicketPlanCut("order-api", "cancel", "read", "order")]);

        nudge.Should().Contain("- order-ui: remove \"and the edit form\" — too big")
            .And.Contain("- order-api: replace \"cancel\" with \"read\" — order")
            .And.EndWith("Re-propose the whole list, in the same shape, every ticket in it, the keys unchanged.");
    }

    private static SpecDialogTurn User(string text) => new(SpecDialogTurn.UserRole, text);

    private static async Task<TicketPlanVerdict?> ReviewAsync(string answer)
    {
        var rig = new TicketPlanReviewRig();
        rig.ReviewerAnswers.Enqueue(answer);
        return await rig.Reviewer().ReviewAsync(Turn(rig.Pipeline), Two(), CancellationToken.None);
    }

    private static ProposalTurn Turn(PipelineContext pipeline) =>
        new(pipeline, new AgenticLoopRequest(new AgentConfig(), TaskType.Planning, "system", "user", []), "user",
            new MasterConversation(), new AgentConfig(), PipelineCostTracker.GetOrCreate(pipeline));

    /// <summary>Searches the named repository once, then answers clean.</summary>
    private sealed class SearchingProvider(string repository) : IChatClient
    {
        private int _turn;

        public string Prompt { get; private set; } = string.Empty;

        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken ct = default)
        {
            if (_turn++ > 0) return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "[]")));
            Prompt = string.Join("\n", messages.Select(m => m.Text));
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant,
                [new FunctionCallContent("call-0", RepositorySearchTool.Name,
                    new Dictionary<string, object?> { ["repository"] = repository, ["pattern"] = "refund" })])));
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken ct = default) => throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose() { }
    }
}
