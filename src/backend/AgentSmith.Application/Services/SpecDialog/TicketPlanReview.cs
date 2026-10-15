using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Loop;
using AgentSmith.Application.Services.Specs.Review;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Turns;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.SpecDialog;

/// <summary>
/// 2026-10-14-dbcek: a proposed list of tickets is reviewed as a PLAN inside the design turn — never spec by spec,
/// because a list carries ticket texts and each ticket's run derives its specs. One review; with spec_review.rounds
/// above 0 and a quoted cut, ONE re-prompt of the design partner, a re-proposal with the same keys published and
/// reviewed once more. Then every ticket:&lt;id&gt; of the final list is read for existence. A conversation that
/// belongs to a ticket is not reviewed: a list is filed from Work it out without a ticket. Nothing here blocks.
/// </summary>
public sealed class TicketPlanReview(
    ISpecReviewSettingsReader settings, TicketPlanReviewer reviewer, ProposalCutsReprompt reprompt,
    ITurnActivityObserverAccessor turnActivity, ILogger<TicketPlanReview> logger,
    ITicketExistence? existence = null) // a host that registers none checks nothing
{
    public const string BoundWhy =
        "a list of tickets is filed from Work it out without a ticket; this conversation continues its own ticket";

    /// <summary>The turn's reply: the one it had, unless the design partner re-proposed the list.</summary>
    public async Task<AgenticLoopResult> ReviewAsync(
        ProposalTurn turn, TicketGraphOutcome graph, AgenticLoopResult loopResult, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(turn);
        ArgumentNullException.ThrowIfNull(graph);
        await turnActivity.ReportAsync(new TurnActivity(TurnActivityKind.Reviewing), ct);
        if (turn.Pipeline.Has(ContextKeys.SpecDialogTicket))
        {
            turn.Pipeline.Set(ContextKeys.SpecDialogOutcome, (OutcomeProposal)(graph with
            {
                Findings = [new ProposalFinding(TicketPlanReviewPrompt.ListKey, "bound conversation", BoundWhy)],
            }));
            return loopResult;
        }
        var (final, reply) = (graph, loopResult);
        var verdict = await reviewer.ReviewAsync(turn, graph, ct);
        if (verdict is { Cuts.Count: > 0 } && Rounds() > 0)
        {
            var revised = await reprompt.RunAsync(turn, TicketPlanReviewPrompt.CutsNudge(verdict.Cuts), ct);
            if (revised?.Proposal is TicketGraphOutcome list && SameKeys(graph, list))
            {
                (final, reply) = (list, MasterOutcomes.PublishOutcome(turn.Pipeline, list, revised.Result));
                verdict = await reviewer.ReviewAsync(turn, list, ct);
            }
            else logger.LogWarning("The re-proposal after the plan review changed the list's keys or was no list; the last valid list stands");
        }
        IReadOnlyList<ProposalFinding> findings = [.. verdict?.Findings ?? [], .. await ExistenceAsync(turn.Pipeline, final, ct)];
        logger.LogInformation("The plan review of the proposed list of {Count} ticket(s) reported {Findings} finding(s)",
            final.Tickets.Count, findings.Count);
        turn.Pipeline.Set(ContextKeys.SpecDialogOutcome, (OutcomeProposal)(final with { Findings = findings }));
        return reply;
    }

    /// <summary>The rounds setting, or 0 when the settings are invalid or unreadable: findings, no re-prompt.</summary>
    private int Rounds()
    {
        try
        {
            var current = settings.Current();
            if (current.Review.Problems() is not { Count: > 0 } problems) return current.Review.Rounds;
            logger.LogWarning("The spec_review settings are invalid ({Problems}); the list is reviewed once", string.Join("; ", problems));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "The spec_review settings could not be read; the list is reviewed once");
        }
        return 0;
    }

    private static bool SameKeys(TicketGraphOutcome before, TicketGraphOutcome after) =>
        before.Tickets.Select(t => t.Key).ToHashSet(StringComparer.Ordinal).SetEquals(after.Tickets.Select(t => t.Key));

    /// <summary>One finding per missing or unreadable ticket:&lt;id&gt;, under each ticket that requires it.</summary>
    private async Task<IReadOnlyList<ProposalFinding>> ExistenceAsync(PipelineContext pipeline, TicketGraphOutcome list, CancellationToken ct)
    {
        var required = list.Tickets.SelectMany(t => t.Requires.Select(r => (t.Key, Id: GraphTicketRequire.ExistingId(r))))
            .Where(r => !string.IsNullOrEmpty(r.Id)).ToList();
        if (existence is null || required.Count == 0
            || !pipeline.TryGet<ResolvedProject>(ContextKeys.SpecDialogProject, out var project) || project is null) return [];
        TicketExistenceResult result;
        try
        {
            result = await existence.CheckAsync(project, [.. required.Select(r => r.Id!).Distinct(StringComparer.Ordinal)], ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "The tickets the proposed list requires could not be checked");
            return [];
        }
        var unreadable = result.Unreadable.ToDictionary(u => u.Id, u => u.Reason, StringComparer.Ordinal);
        return [.. required.Select(r => result.Missing.Contains(r.Id!)
                ? new ProposalFinding(r.Key, "required ticket not found", $"ticket {r.Id} does not exist on {project.Tracker.Name}")
                : unreadable.TryGetValue(r.Id!, out var reason)
                    ? new ProposalFinding(r.Key, "required ticket not read", $"ticket {r.Id} could not be read ({reason})")
                    : null).OfType<ProposalFinding>()];
    }
}
