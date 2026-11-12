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
/// messages whole, its open questions, the uploads by their reference: address, readable through its look.
/// </summary>
public sealed class TicketPlanReviewerTests
{
    private static TicketGraphOutcome Two() => List(Ticket("order-api"), Ticket("order-ui", "order-api"));

    [Fact]
    public async Task TicketPlanReviewer_FindingForUnknownTicket_Dropped()
    {
        var findings = await ReviewAsync(Finding("order-export", "merge"));

        findings.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public async Task TicketPlanReviewer_FindingOnATicket_KeptUnderItsKey()
    {
        var findings = await ReviewAsync(Finding("order-ui", "size"));

        findings!.Should().ContainSingle().Which.Should().Match<ProposalFinding>(f => f.SpecId == "order-ui" && f.Why == "size on order-ui");
    }

    [Fact]
    public async Task TicketPlanReviewer_MissingRequirement_IsAListFinding()
    {
        var findings = await ReviewAsync(Finding("order-ui", TicketPlanReviewPrompt.Missing));

        findings!.Should().ContainSingle().Which.SpecId.Should().Be(TicketPlanReviewPrompt.ListKey);
    }

    [Fact]
    public async Task TicketPlanReviewer_OpenQuestions_AreShownToTheReviewer()
    {
        var rig = new TicketPlanReviewRig();
        rig.Pipeline.Set(ContextKeys.SpecDialogReplySlot, new SpecDialogReplySlot
        {
            Questions = [new OpenQuestion("Who owns refunds?", "Accounting.", [])],
        });

        await rig.Reviewer().ReviewAsync(Turn(rig.Pipeline), Two(), CancellationToken.None);

        rig.ReviewerPrompts.Should().ContainSingle().Which.Should().Contain("- Who owns refunds?").And.Contain("proposed: Accounting.");
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
    public void TicketPlanReviewPrompt_For_NamesTheUploadsAndAsksFiveQuestionsWithoutCuts()
    {
        var prompt = TicketPlanReviewPrompt.For(Two(), [new SpecDialogTurn(SpecDialogTurn.UserRole, "Orders need refunds.")], null,
            ["reference:requirements"], []);

        prompt.Should().Contain("- reference:requirements").And.Contain("Orders need refunds.")
            .And.Contain($"5. {TicketPlanReviewPrompt.Owner}").And.Contain("key: order-ui").And.Contain("requires: order-api")
            .And.NotContain("quote").And.NotContain("replacement").And.NotContain("the keys stay");
    }

    [Fact]
    public void TicketPlanReviewPrompt_LongTranscript_ShowsEveryMessage()
    {
        var big = new string('x', 15_000);
        IReadOnlyList<SpecDialogTurn> transcript =
        [
            User("FIRST " + big), new SpecDialogTurn(SpecDialogTurn.AssistantRole, "noted"), User("SECOND " + big),
            User("THIRD " + big), User("FOURTH " + big),
        ];

        var requirements = TicketPlanReviewPrompt.Requirements(transcript);

        requirements.Should().Contain("FIRST ").And.Contain("SECOND ").And.Contain("THIRD ").And.Contain("FOURTH ")
            .And.NotContain("noted").And.NotContain("left out");
    }

    [Fact]
    public void TicketPlanReviewPrompt_OpenQuestions_AreShown()
    {
        var prompt = TicketPlanReviewPrompt.For(Two(), [], null, [], [new OpenQuestion("Keep cancelled orders?", "No.", [])]);

        prompt.Should().Contain("QUESTIONS THE OPERATOR STILL ANSWERS").And.Contain("- Keep cancelled orders?").And.Contain("proposed: No.");
    }

    private static SpecDialogTurn User(string text) => new(SpecDialogTurn.UserRole, text);

    private static async Task<IReadOnlyList<ProposalFinding>?> ReviewAsync(string answer)
    {
        var rig = new TicketPlanReviewRig();
        rig.ReviewerAnswers.Enqueue(answer);
        return await rig.Reviewer().ReviewAsync(Turn(rig.Pipeline), Two(), CancellationToken.None);
    }

    private static ProposalTurn Turn(PipelineContext pipeline) =>
        new(pipeline, new AgentConfig(), PipelineCostTracker.GetOrCreate(pipeline));

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
