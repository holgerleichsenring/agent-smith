using AgentSmith.Contracts.Specs;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.Tickets;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// 2026-09-25-8e51d: the specification a person approved for the ticket a conversation BELONGS
/// to, projected into the shape the pane draws.
/// <para>
/// The ticket is read off the SESSION ROW and never off a request: the reads on this surface are
/// addressed by dialog id precisely so that nobody can follow a ticket by asking for it. And it
/// is keyed by the conversation's own project's tracker connection, because the spec key carries
/// only the tracker TYPE — a guessed instance would hand the conversation another organisation's
/// record.
/// </para>
/// <para>
/// 2026-11-17-a0b6c: the record says WHOSE approval and when; the specs are the ticket branch's
/// (<see cref="TicketBranchSpecs"/>), so the pane never shows a second copy that disagrees with what a run reads.
/// The branch is addressed by the record's project and ticket id, else the session's project and the key's id part.
/// </para>
/// </summary>
public sealed class ApprovedSetForConversation(
    SpecDialogSessionRepository sessions,
    ISpecApprovalStore approvals,
    TicketBranchSpecs branch)
{
    public async Task<ApprovedSetView?> ForAsync(string dialogId, CancellationToken ct)
    {
        var session = await sessions.GetOpenByThreadAsync(
            DispatcherDefaults.PlatformDashboard, dialogId, ct);
        if (session?.Tracker is not { } tracker || session.TicketKey is not { } key) return null;
        var record = await approvals.GetAsync(tracker, key, ct);
        if (record is null) return null;
        var read = await branch.ReadAsync(
            NonEmpty(record.Project) ?? session.Project, NonEmpty(record.TicketId) ?? IdOf(key), ct);
        return View(record, tracker, read);
    }

    private static ApprovedSetView View(SpecApprovalRecord record, string tracker, TicketBranchRead read) => new(
        record.Key,
        tracker,
        record.Set.Approval?.At,
        record.Set.Approval?.Principal,
        record.Set.Approval?.Conversation,
        record.Repositories,
        [.. (read.Set?.Specs ?? []).Select(spec => SpecDialogProposalComposer.View(spec.Draft))],
        read.Why);

    // The key is <provider>-<ticketId>; an older record carries no id of its own.
    private static string IdOf(string key) => key.IndexOf('-') is var dash and > 0 ? key[(dash + 1)..] : key;

    private static string? NonEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
