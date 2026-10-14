using AgentSmith.Contracts.Commands;
using AgentSmith.Server.Models;

namespace AgentSmith.Server.Services.Tickets.Text;

/// <summary>
/// The ticket text of one ticket page: the tracker's answer first; when the tracker cannot be read or the project
/// is no longer configured, the newest copy one of its code attempts kept; with neither, unavailable and why.
/// </summary>
public sealed class TicketTextReader(
    TicketRunsQuery runQuery,
    TicketAttempts attempts,
    TicketProjects projects,
    TicketTrackerTextReader tracker,
    TicketTextSnapshotReader snapshots)
{
    /// <summary>Null when the pair has no code run — it is not a ticket.</summary>
    public async Task<TicketTextView?> ReadAsync(string project, string ticketId, CancellationToken ct)
    {
        var code = (await runQuery.ForTicketAsync(project, ticketId, ct))
            .Where(r => r.Run.Pipeline == PipelinePresets.CodeName).ToList();
        if (code.Count == 0) return null;
        var read = projects.Of(project) is { } resolved
            ? await tracker.ReadAsync(resolved, ticketId, ct)
            : null;
        if (read?.Ticket is { } ticket) return TicketTextView.FromTracker(ticket, read.At);
        var ordered = attempts.Of(code).Select(a => a.Run).ToList();
        return await snapshots.ReadAsync(ordered, ct)
            ?? TicketTextView.Unavailable(read?.Reason ?? TicketTextReasons.ProjectNotConfigured);
    }
}
