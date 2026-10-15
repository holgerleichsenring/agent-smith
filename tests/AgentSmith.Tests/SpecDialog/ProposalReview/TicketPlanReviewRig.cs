using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Events;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Loop;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Application.Services.Specs.Review;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Turns;
using AgentSmith.Tests.Handlers;
using AgentSmith.Tests.Specs;
using AgentSmith.Tests.TestHelpers;
using AgentSmith.Tests.Validation;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.SpecDialog.ProposalReview;

/// <summary>
/// 2026-10-14-dbcek: the plan review of a proposed list as the product composes it — the real review, reviewer,
/// re-prompt and resolver — over a scripted reviewer model, a scripted design partner and a scripted existence read.
/// </summary>
internal sealed class TicketPlanReviewRig
{
    public PipelineContext Pipeline { get; } = new();
    public SpecReviewSettings Settings { get; set; } = ProposalReviewRig.Of(rounds: 4);
    public Queue<string> ReviewerAnswers { get; } = new();
    public List<string> ReviewerPrompts { get; } = [];
    public Exception? ReviewerFails { get; set; }
    public Queue<string> PartnerReplies { get; } = new();
    public List<string> Nudges { get; } = [];
    public ScriptedExistence? Existence { get; set; }
    public Action? OnReviewerCall { get; set; }

    /// <summary>The review the other turn rigs construct; it is never reached unless a turn proposes a list.</summary>
    public static TicketPlanReview Default() => new TicketPlanReviewRig().Review();

    public static GraphTicket Ticket(string key, params string[] requires) => TicketGraphFixtures.Ticket(key, requires);

    public static TicketGraphOutcome List(params GraphTicket[] tickets) => new(tickets);

    /// <summary>The design partner's reply re-proposing a list, every string quoted.</summary>
    public static string ListReply(params (string Key, string Requires)[] tickets) =>
        TicketGraphFixtures.Block(string.Concat(tickets.Select(t => TicketGraphFixtures.Item(t.Key, t.Requires))));

    public TicketPlanReview Review(ITurnActivityObserverAccessor? activity = null, ISpecReviewSettingsReader? reader = null) =>
        new(reader ?? ProposalReviewRig.Reader(() => Settings), Reviewer(),
            new ProposalCutsReprompt(Partner(), MasterHandlerFixture.BuildOutcomeResolver(), TurnActivityRecorder.Silent(),
                NullLogger<ProposalCutsReprompt>.Instance),
            activity ?? TurnActivityRecorder.Silent(), NullLogger<TicketPlanReview>.Instance, Existence);

    /// <summary>The scripted reviewer model, for a composition that builds the reviewer itself.</summary>
    public IChatClientFactory Factory() => new ScriptedFactory(this);

    public TicketPlanReviewer Reviewer() =>
        new(new ScriptedFactory(this), new AsyncLocalRunContextAccessor(), DerivationTestLooks.Factory(),
            NullLogger<TicketPlanReviewer>.Instance);

    public async Task<AgenticLoopResult> RunAsync(TicketGraphOutcome graph, TicketPlanReview? review = null)
    {
        Pipeline.Set(ContextKeys.SpecDialogOutcome, (OutcomeProposal)graph);
        var request = new AgenticLoopRequest(new AgentConfig(), TaskType.Planning, "system", "user", []);
        var first = ProposalReviewRig.Result("Here is the list.");
        var conversation = new MasterConversation();
        conversation.Opened(request, first.Response);
        var turn = new ProposalTurn(Pipeline, request, "user", conversation, new AgentConfig(), PipelineCostTracker.GetOrCreate(Pipeline));
        return await (review ?? Review()).ReviewAsync(turn, graph, first, CancellationToken.None);
    }

    public TicketGraphOutcome Outcome => (TicketGraphOutcome)Pipeline.Get<OutcomeProposal>(ContextKeys.SpecDialogOutcome);

    /// <summary>A reviewer answer of one finding, as JSON.</summary>
    public static string Finding(string ticket, string problem, string? quote = null, string? replacement = null) =>
        System.Text.Json.JsonSerializer.Serialize(new[] { new { ticket, problem, why = $"{problem} on {ticket}", quote, replacement } });

    private IAgenticLoopRunner Partner()
    {
        var runner = new Mock<IAgenticLoopRunner>();
        runner.Setup(r => r.RunAsync(It.IsAny<AgenticLoopRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AgenticLoopRequest request, CancellationToken _) =>
            {
                Nudges.Add(request.UserPrompt);
                if (PartnerReplies.Count == 0) throw new InvalidOperationException("the design partner has no scripted reply");
                return ProposalReviewRig.Result(PartnerReplies.Dequeue());
            });
        return runner.Object;
    }

    private sealed class ScriptedFactory(TicketPlanReviewRig rig) : IChatClientFactory
    {
        public IChatClient Create(AgentConfig agent, TaskType task, int? maxIterations = null, MasterLoopHooks? masterLoopHooks = null)
        {
            var client = new Mock<IChatClient>();
            client.Setup(c => c.GetResponseAsync(It.IsAny<IEnumerable<ChatMessage>>(), It.IsAny<ChatOptions?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((IEnumerable<ChatMessage> messages, ChatOptions? _, CancellationToken _) =>
                {
                    rig.ReviewerPrompts.Add(string.Join("\n", messages.Select(m => m.Text)));
                    rig.OnReviewerCall?.Invoke();
                    if (rig.ReviewerFails is { } fails) throw fails;
                    var answer = rig.ReviewerAnswers.Count == 0 ? "[]" : rig.ReviewerAnswers.Dequeue();
                    return new ChatResponse(new ChatMessage(ChatRole.Assistant, answer));
                });
            return client.Object;
        }

        public int GetMaxOutputTokens(AgentConfig agent, TaskType task) => 4096;

        public string GetModel(AgentConfig agent, TaskType task) => "stub-model";
    }

    /// <summary>Answers missing and unreadable ids as scripted and records every check.</summary>
    public sealed class ScriptedExistence(IReadOnlyList<string> missing, params UnreadableTicket[] unreadable) : ITicketExistence
    {
        public List<IReadOnlyList<string>> Checked { get; } = [];

        public Task<TicketExistenceResult> CheckAsync(ResolvedProject project, IReadOnlyList<string> ids, CancellationToken ct)
        {
            Checked.Add(ids);
            return Task.FromResult(new TicketExistenceResult(
                [.. missing.Where(ids.Contains)], [.. unreadable.Where(u => ids.Contains(u.Id))]));
        }
    }
}
