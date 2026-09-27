using AgentSmith.Contracts.Specs;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Models;

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
/// </summary>
public sealed class ApprovedSetForConversation(
    SpecDialogSessionRepository sessions,
    ISpecApprovalStore approvals)
{
    public async Task<ApprovedSetView?> ForAsync(string dialogId, CancellationToken ct)
    {
        var session = await sessions.GetOpenByThreadAsync(
            DispatcherDefaults.PlatformDashboard, dialogId, ct);
        if (session?.Tracker is not { } tracker || session.TicketKey is not { } key) return null;
        var record = await approvals.GetAsync(tracker, key, ct);
        return record is null ? null : View(record, tracker);
    }

    private static ApprovedSetView View(SpecApprovalRecord record, string tracker) => new(
        record.Key,
        tracker,
        record.Set.Approval?.At,
        record.Set.Approval?.Principal,
        record.Set.Approval?.Conversation,
        record.Repositories,
        [.. record.Set.Phases.Select(phase => SpecDialogProposalComposer.View(phase.Draft))]);
}
