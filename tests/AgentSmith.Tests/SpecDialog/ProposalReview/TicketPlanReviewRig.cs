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
/// 2026-10-14-dbcek: the plan review of a proposed list as the product composes it — the real post-turn review, pass,
/// plan review and reviewer — over a scripted reviewer model and a scripted existence read. 2026-11-12-cd4ci: the
/// design turn's pass is on unless a test turns it off; nothing re-prompts.
/// </summary>
internal sealed class TicketPlanReviewRig
{
    public PipelineContext Pipeline { get; } = new();
    public SpecReviewSettings Settings { get; set; } = ProposalReviewRig.Of(rounds: 4);
    public Queue<string> ReviewerAnswers { get; } = new();
    public List<string> ReviewerPrompts { get; } = [];
    public Exception? ReviewerFails { get; set; }
    public ScriptedExistence? Existence { get; set; }
    public Action? OnReviewerCall { get; set; }

    /// <summary>The review the other turn rigs construct; it is never reached unless a turn proposes a list.</summary>
    public static TicketPlanReview Default() => new TicketPlanReviewRig().Review();

    public static GraphTicket Ticket(string key, params string[] requires) => TicketGraphFixtures.Ticket(key, requires);

    public static TicketGraphOutcome List(params GraphTicket[] tickets) => new(tickets);

    /// <summary>The design partner's reply proposing a list, every string quoted.</summary>
    public static string ListReply(params (string Key, string Requires)[] tickets) =>
        TicketGraphFixtures.Block(string.Concat(tickets.Select(t => TicketGraphFixtures.Item(t.Key, t.Requires))));

    public TicketPlanReview Review() =>
        new(Reviewer(), new TicketExistenceFindings(NullLogger<TicketExistenceFindings>.Instance, Existence),
            NullLogger<TicketPlanReview>.Instance);

    /// <summary>The post-turn review over this rig's plan review, reporting to <paramref name="activity"/>.</summary>
    public SpecDialogProposalReview Turn(ITurnActivityObserverAccessor? activity = null, ISpecReviewSettingsReader? reader = null)
    {
        var plans = Review();
        return new(new ProposalMechanicalChecks(ProposalEvidenceTestReview.Create(), plans, NullLogger<ProposalMechanicalChecks>.Instance),
            reader ?? ProposalReviewRig.Reader(() => Settings), new ProposalReviewRig().Pass(activity: activity, plans: plans),
            NullLogger<SpecDialogProposalReview>.Instance);
    }

    /// <summary>The scripted reviewer model, for a composition that builds the reviewer itself.</summary>
    public IChatClientFactory Factory() => new ScriptedFactory(this);

    public TicketPlanReviewer Reviewer() =>
        new(new ScriptedFactory(this), new AsyncLocalRunContextAccessor(), DerivationTestLooks.Factory(),
            NullLogger<TicketPlanReviewer>.Instance);

    public async Task RunAsync(TicketGraphOutcome graph, SpecDialogProposalReview? review = null)
    {
        var reviewed = await (review ?? Turn()).ReviewAsync(
            new ProposalTurn(Pipeline, new AgentConfig(), PipelineCostTracker.GetOrCreate(Pipeline)), graph, CancellationToken.None);
        Pipeline.Set(ContextKeys.SpecDialogOutcome, reviewed.Reviewed);
    }

    public TicketGraphOutcome Outcome => (TicketGraphOutcome)Pipeline.Get<OutcomeProposal>(ContextKeys.SpecDialogOutcome);

    /// <summary>A reviewer answer of one finding, as JSON.</summary>
    public static string Finding(string ticket, string problem) =>
        System.Text.Json.JsonSerializer.Serialize(new[] { new { ticket, problem, why = $"{problem} on {ticket}" } });

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
