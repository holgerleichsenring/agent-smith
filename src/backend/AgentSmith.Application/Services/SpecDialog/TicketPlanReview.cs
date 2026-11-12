using AgentSmith.Application.Services.Specs.Review;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.SpecDialog;

/// <summary>
/// 2026-10-14-dbcek: a proposed list of tickets is reviewed as a PLAN — never spec by spec, because a list carries
/// ticket texts and each ticket's run derives its specs. 2026-11-12-cd4ci: ONE advisory reviewer call, its findings on
/// the card, nothing re-prompted; <see cref="CheckAsync"/> is the mechanical half alone. A conversation that belongs
/// to a ticket is not reviewed: a list is filed from Work it out without a ticket. Nothing here blocks.
/// </summary>
public sealed class TicketPlanReview(
    TicketPlanReviewer reviewer, TicketExistenceFindings existence, ILogger<TicketPlanReview> logger)
{
    /// <summary>The problem of the one finding a list in a ticket's own conversation gets: nothing reviewed it.</summary>
    public const string BoundProblem = "bound conversation";

    public const string BoundWhy =
        "a list of tickets is filed from Work it out without a ticket; this conversation continues its own ticket";

    /// <summary>The list with the plan reviewer's findings and the existence check's.</summary>
    public async Task<OutcomeProposal> ReviewAsync(ProposalTurn turn, TicketGraphOutcome graph, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(turn);
        ArgumentNullException.ThrowIfNull(graph);
        if (Bound(turn.Pipeline, graph) is { } bound) return bound;
        var findings = await reviewer.ReviewAsync(turn, graph, ct) ?? [];
        IReadOnlyList<ProposalFinding> all = [.. findings, .. await existence.FindingsAsync(turn.Pipeline, graph, ct)];
        logger.LogInformation("The plan review of the proposed list of {Count} ticket(s) reported {Findings} finding(s)",
            graph.Tickets.Count, all.Count);
        return graph with { Findings = all };
    }

    /// <summary>The list with the existence check's findings only; no model call.</summary>
    public async Task<OutcomeProposal> CheckAsync(PipelineContext pipeline, TicketGraphOutcome graph, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(graph);
        return Bound(pipeline, graph) ?? graph with { Findings = await existence.FindingsAsync(pipeline, graph, ct) };
    }

    private static OutcomeProposal? Bound(PipelineContext pipeline, TicketGraphOutcome graph) =>
        pipeline.Has(ContextKeys.SpecDialogTicket)
            ? graph with { Findings = [new ProposalFinding(TicketPlanReviewPrompt.ListKey, BoundProblem, BoundWhy)] }
            : null;
}
