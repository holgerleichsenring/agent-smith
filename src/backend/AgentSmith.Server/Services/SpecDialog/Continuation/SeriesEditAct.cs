using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Specs;
using AgentSmith.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.SpecDialog.Continuation;

/// <summary>
/// An edit of a worked ticket's series asked for in its conversation: an approval without a start. The edited list
/// is decided exactly as a card approval of an epic is (<see cref="TicketContinuation.Decide"/>) and, on continue,
/// recorded with the poster as principal and no start, then the ticket's activity is nudged. A closed or
/// never-worked ticket, an empty basis, a waiting question, an over-cap or an unordered list records nothing.
/// </summary>
public sealed class SeriesEditAct(
    NextAttemptSeriesReader series, SeriesEditList list, SpecDialogSessionRepository sessions,
    ApprovedSpecSetRecorder approvals, ITicketActivityNudge activity, Application.Services.Specs.ISeriesSettingsReader settings,
    ILogger<SeriesEditAct> logger)
{
    /// <summary>The sentence the conversation is told.</summary>
    public async Task<string> EditAsync(ResolvedProject project, string ticketId, string conversation, string principal,
        IReadOnlyList<SeriesEdit> edits, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(project);
        var next = await series.ReadAsync(project, ticketId, mayStartRuns: true, ct);
        if (next.Read.State is not { } state) return next.Read.Failure!;
        if (Refusal(state) is { } refused) return refused;
        var (drafts, error) = list.Apply(next.Specs, edits);
        if (error is not null) return $"Nothing was recorded: {error}.";
        var goal = state.Record?.Set.Goal ?? drafts.FirstOrDefault()?.Goal ?? string.Empty;
        // The edited list is the whole series, so the previous record's ids need not stay reserved: an id it
        // appended is minted the same again.
        var decision = TicketContinuation.Decide(state with { Record = null },
            new EpicOutcome(new SpecDraft(state.Series ?? ticketId, goal, string.Empty, []), drafts), settings.Current());
        if (decision.Effect != TicketContinuationEffects.Continue) return decision.Reason;
        if (decision.Start is { Kind: TicketStartArms.QuestionPending } waiting)
            return $"Run {waiting.RunId} is waiting for an answer to its question, so nothing was recorded: answer it first, then edit again.";
        if (await sessions.GetBySessionIdAsync(conversation, ct) is not { } session)
            return "This conversation is no longer stored, so the edit has no approval to record. Nothing was recorded.";
        var edited = decision with { Start = null };
        try
        {
            await approvals.RecordAsync(SpecDialogSessionMapper.ToState(session) with { UserId = principal }, project, ticketId,
                new FiledSeries(state.Series ?? string.Empty, edited.Drafts), goal, ct, edited.Continuation, state.Scope);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogError(ex, "Recording the series edit of ticket {Ticket} failed", ticketId);
            return $"The edit of ticket {ticketId}'s series was not recorded: {ex.Message}.";
        }
        logger.LogInformation("Ticket {Ticket}'s series edited by {Principal} in conversation {Conversation}", ticketId, principal, conversation);
        await activity.ChangedAsync(project.Name, TicketKey.For(project.Tracker.Type.ToString().ToLowerInvariant(), ticketId).Value, ct);
        return Answer(edited);
    }

    private static string? Refusal(TicketContinuationState state)
    {
        if (state.ClosedForWork)
            return $"Ticket {state.TicketId} was closed by a person, so its series is not edited here. Nothing was recorded.";
        if (!state.Worked)
            return $"No code attempt has worked ticket {state.TicketId}; its series is formed in Work it out. Nothing was recorded.";
        return state.Basis.Count == 0
            ? $"No attempt of ticket {state.TicketId} published a series and no approval recorded one, so its next attempt "
                + "could not take an edit over. Nothing was recorded."
            : null;
    }

    private static string Answer(TicketContinuationDecision decision) =>
        $"Recorded for the next attempt; nothing was started. {decision.Reason}\n" + string.Join("\n", decision.Specs.Select(s =>
            $"- {s.SpecId}: {s.Disposition}{(s.Note is null ? string.Empty : $" ({s.Note})")} — {s.Goal}"));
}
