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
    TicketTextSnapshotReader snapshots,
    Checks.TicketCheckKicks kicks)
{
    /// <summary>Null when the tracker does not answer for a pair with no code run. 2026-11-17-dd05d: a filed ticket no
    /// run worked is read from its tracker too. 2026-11-22-a720b: a tracker answer offers the ticket to the text edit
    /// check, which runs off this request and never answers it.</summary>
    /// <param name="fresh">Past the tracker read's cache.</param>
    public async Task<TicketTextView?> ReadAsync(string project, string ticketId, CancellationToken ct, bool fresh = false)
    {
        var code = (await runQuery.ForTicketAsync(project, ticketId, ct))
            .Where(r => r.Run.Pipeline == PipelinePresets.CodeName).ToList();
        var resolved = projects.Of(project);
        var read = resolved is null ? null : await tracker.ReadAsync(resolved, ticketId, ct, fresh);
        if (read?.Ticket is { } ticket)
        {
            kicks.Offer([new Checks.TicketCheckKick(project, ticketId, Checks.TicketCheckKind.TextEdit)]);
            return TicketTextView.FromTracker(ticket, read.At);
        }
        if (code.Count == 0) return null;
        var ordered = attempts.Of(code).Select(a => a.Run).ToList();
        return await snapshots.ReadAsync(ordered, ct)
            ?? TicketTextView.Unavailable(read?.Reason ?? TicketTextReasons.ProjectNotConfigured);
    }
}
