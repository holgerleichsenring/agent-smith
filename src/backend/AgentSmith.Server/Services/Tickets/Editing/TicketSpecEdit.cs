using AgentSmith.Application.Services.Filing;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Server.Services.SpecDialog;
using AgentSmith.Server.Services.SpecDialog.Continuation;
using Microsoft.AspNetCore.Http;

namespace AgentSmith.Server.Services.Tickets.Editing;

/// <summary>
/// 2026-11-17-dd05c: a person edits one spec of a filed ticket from its page. The edit is the series as it stands with
/// that spec re-cut, approved by the editor, and it goes where a conversation's approval goes: a ticket no attempt
/// worked is amended (an 'amend' run writes body, record and branch), a worked one keeps it for its next attempt. A
/// closed ticket and a spec that already ran are not edited; nothing here writes a spec any other way.
/// </summary>
public sealed class TicketSpecEdit(
    TicketProjects projects, NextAttemptSeriesReader series, SeriesEditList list, TicketAmendment amendment, SeriesEditAct edits)
{
    public async Task<TicketSpecEditAnswer> EditAsync(
        string projectName, string ticketId, string specId, string yaml, string principal, CancellationToken ct)
    {
        if (projects.Of(projectName) is not { } project)
            return new(StatusCodes.Status404NotFound, $"No project named '{projectName}' is configured.");
        var next = await series.ReadAsync(project, ticketId, mayStartRuns: false, ct);
        if (next.Read.State is not { } state) return Refused(next.Read.Failure!);
        if (Refusal(state, ticketId, specId) is { } refused) return Refused(refused);
        var edit = new SeriesEdit(SeriesEditKinds.Recut, specId, yaml);
        var source = Source(state, principal);
        if (state.Worked)
        {
            var kept = await edits.EditAsync(project, ticketId, source, [edit], ct);
            return kept.Recorded ? new(StatusCodes.Status200OK, kept.Message) : Refused(kept.Message);
        }
        var (drafts, error) = list.Apply(next.Specs, [edit]);
        if (error is not null) return Refused($"Nothing was recorded: {error}.");
        var amended = await amendment.ApplyAsync(project, ticketId, source, Proposal(state, ticketId, drafts), ct);
        return amended.Recorded ? new(StatusCodes.Status200OK, amended.Notice) : Refused(amended.Notice);
    }

    private static string? Refusal(TicketContinuationState state, string ticketId, string specId)
    {
        if (state.ClosedForWork) return $"Ticket {ticketId} was closed by a person, so its specs are not edited. Reopen it first.";
        return state.ExecutedIds.Contains(specId, StringComparer.Ordinal)
            ? $"Spec {specId} already ran on ticket {ticketId}, and a spec that ran never changes: ask for the correction "
                + "in the ticket's conversation, where it becomes a new spec."
            : null;
    }

    // The editor approves the series as it now reads; the approval it replaces keeps naming its conversation, scope and words.
    private static ApprovalSource Source(TicketContinuationState state, string principal) => new(
        state.Record?.Set.Approval?.Conversation ?? string.Empty, principal,
        state.Record?.Repositories ?? [], state.Record?.OwnerWords ?? []);

    private static OutcomeProposal Proposal(TicketContinuationState state, string ticketId, IReadOnlyList<SpecDraft> drafts) =>
        drafts.Count == 1 ? new SpecOutcome(drafts[0])
            : new EpicOutcome(SeriesParentDraft.Of(state.Series ?? ticketId, state.Record?.Set.Goal ?? drafts[0].Goal), drafts,
                state.Record?.Set.Contract);

    private static TicketSpecEditAnswer Refused(string message) => new(StatusCodes.Status409Conflict, message);
}
