using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Specs;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Models;
using Microsoft.Extensions.Logging;
using static AgentSmith.Server.Services.SpecDialog.Continuation.TicketContinuationEffects;

namespace AgentSmith.Server.Services.SpecDialog.Continuation;

/// <summary>
/// 2026-10-13-9a59l: approving a spec or an epic in a ticket's OWN conversation. Nothing new is filed
/// for a ticket that is still open: the decision is read once, and its effect applied — a continuation
/// recorded under the ticket for its next attempt, a never-worked ticket's series recorded whole, or a
/// follow-up filed for a ticket a person closed. A refusal records nothing and keeps the proposal.
/// <para>2026-10-13-9a59m: the decision's start arm then starts the attempt (<see cref="OperatorStart"/>); a
/// question that waits refuses the approval like a refusal does, so it can be approved once answered.</para>
/// </summary>
public sealed class TicketContinuationAct(
    AgentSmithConfig config, SpecDialogTicketTextRepository ticketText, TicketContinuationStateReader reader,
    TicketAmendment amendment, ClosedTicketFollowUp followUp, ApprovedSpecSetRecorder approvals,
    ITicketActivityNudge activity, OperatorStart starter, ILogger<TicketContinuationAct> logger)
{
    public async Task<TicketContinuationOutcome> ApplyAsync(
        ConversationState state, OutcomeProposal proposal, bool mayStartRuns, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(state);
        var name = state.Scope?.Project ?? state.Project;
        if (await ticketText.GetAsync(state.JobId, ct) is not { TicketId: { Length: > 0 } ticketId })
            return new("This conversation never recorded which ticket it belongs to, so the approval has no "
                + "ticket to continue. Nothing was recorded.", false);
        var read = await reader.ReadAsync(name, ticketId, mayStartRuns, ct);
        if (read.State is not { } current) return new(read.Failure!, false);
        var decision = TicketContinuation.Decide(current, proposal);
        logger.LogInformation("Approval in conversation {Session} on ticket {Ticket}: {Effect}", state.JobId, ticketId, decision.Effect);
        switch (decision.Effect)
        {
            case AmendNeverWorked:
                var amended = await amendment.ApplyAsync(state, proposal, ct,
                    decision.Start?.StartsAttempt == true ? new SeriesContinuation([], null, StartsAttempt: true) : null);
                return amended.Recorded && decision.Start is { } first
                    ? new($"{amended.Notice} {await starter.StartAsync(config.Projects[name], ticketId, decision.Effect, first, ct)}", true)
                    : new(amended.Notice, amended.Recorded);
            case FollowUpClosed:
                return await followUp.FileAsync(state, proposal, decision, config.Projects[name], ticketId, mayStartRuns, ct);
            case Continue when decision.Start is { Kind: TicketStartArms.QuestionPending } waiting:
                return new(OperatorStartArm.Say(waiting), false);
            case Continue:
                return await ContinueAsync(state, proposal, decision, current, config.Projects[name], ticketId, ct);
            default:
                return new(decision.Reason, false);
        }
    }

    private async Task<TicketContinuationOutcome> ContinueAsync(
        ConversationState state, OutcomeProposal proposal, TicketContinuationDecision decision,
        TicketContinuationState current, ResolvedProject project, string ticketId, CancellationToken ct)
    {
        var goal = proposal is EpicOutcome epic ? epic.Parent.Goal
            : current.Record?.Set.Goal ?? decision.Drafts.FirstOrDefault()?.Goal ?? string.Empty;
        var contract = ContinuedContract.Of(proposal, decision, current); // 2026-10-13-8762j
        if (contract.Removed.Count > 0)
            logger.LogInformation("Ticket {Ticket}: contract entries naming no spec of the continued series removed: {Removed}", ticketId, string.Join("; ", contract.Removed));
        try
        {
            await approvals.RecordAsync(state, project, ticketId, new FiledSeries(current.Series ?? string.Empty, decision.Drafts)
                { Contract = contract.Contract, ReviewRounds = ApprovedReviewRounds.Rekey(proposal.ReviewRounds, decision.Ids, logger, ticketId) },
                goal, ct, decision.Continuation, current.Scope);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogError(ex, "Recording the continuation of ticket {Ticket} failed", ticketId);
            return new($"The approval of ticket {ticketId} was not recorded: {ex.Message}. Approve again.", false);
        }
        await activity.ChangedAsync(project.Name, TicketKey.For(project.Tracker.Type.ToString().ToLowerInvariant(), ticketId).Value, ct);
        var started = decision.Start is { } start ? $" {await starter.StartAsync(project, ticketId, decision.Effect, start, ct)}" : string.Empty;
        return new($"{decision.Reason} {Summary(decision)}{started}", true);
    }

    private static string Summary(TicketContinuationDecision decision) => string.Join(" ",
        new[] { SpecDispositionKinds.Recut, SpecDispositionKinds.Appended, SpecDispositionKinds.Dropped }
            .Select(kind => (kind, ids: decision.Specs.Where(s => s.Disposition == kind).Select(s => s.SpecId).ToList()))
            .Where(k => k.ids.Count > 0)
            .Select(k => $"{Word(k.kind)}: {string.Join(", ", k.ids)}."));

    private static string Word(string kind) => kind switch
    {
        SpecDispositionKinds.Recut => "Re-cut",
        SpecDispositionKinds.Appended => "Appended",
        _ => "Dropped",
    };
}
