using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Specs;

namespace AgentSmith.Server.Services.SpecDialog.Continuation;

/// <summary>
/// The ticket's series edit that no attempt has worked yet: the stored approval record when it continues an
/// attempt (it has a BasisRunId) and its approval is newer than the approval the latest attempt published as
/// worked. That is the record the next attempt merges over the branch series; null when there is none.
/// </summary>
public sealed class PendingSeriesEditReader(ISpecApprovalStore approvals, IPreviousAttemptReader attempts)
{
    public async Task<SpecApprovalRecord?> ReadAsync(ResolvedProject project, string ticketId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(project);
        var key = TicketKey.For(project.Tracker.Type.ToString().ToLowerInvariant(), ticketId).Value;
        var record = await approvals.GetAsync(project.Tracker.Name, key, ct);
        if (record?.Continuation is not { BasisRunId: not null } || record.Approval is not { } approval) return null;
        var latest = await attempts.LatestAsync(project.Name, ticketId, null, ct);
        return latest?.WorkedApprovalAt is { } worked && approval.At <= worked ? null : record;
    }
}
