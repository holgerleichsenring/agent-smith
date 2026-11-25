using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Specs;

namespace AgentSmith.Application.Services.Series;

/// <summary>
/// 2026-11-25-433fa: at a beat's end, the one ticket nudge (<see cref="ITicketActivityNudge"/>) for every member whose kind
/// changed against the table the last beat saw, and for every member when the beat moved the series' state. The series page
/// re-reads on its project's ticket nudges, so its status line and graph move in place; a run's end raises no nudge of its
/// own, the table change that starts this beat is where it is noticed. A beat that changed nothing nudges nothing.
/// </summary>
public sealed class SeriesTicketNudges(ITicketActivityNudge activity)
{
    public async Task SendAsync(SeriesBeat beat, ResolvedProject project, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(beat);
        ArgumentNullException.ThrowIfNull(project);
        var tracker = project.Tracker.Type.ToString().ToLowerInvariant(); // TicketKeyResolver's rule for a project's tickets
        foreach (var ticketId in Changed(beat))
            await activity.ChangedAsync(beat.Record.Project, TicketKey.For(tracker, ticketId).Value, cancellationToken);
    }

    /// <summary>The ids of the members whose page changed in the beat, in the series' order.</summary>
    public static IReadOnlyList<string> Changed(SeriesBeat beat)
    {
        ArgumentNullException.ThrowIfNull(beat);
        if (beat.Record.State != beat.StartState) return [.. beat.Facts.Select(f => f.Ticket.TicketId)];
        var before = SeriesTable.Parse(beat.Record.LastTable);
        return [.. beat.Facts.Where(f => !before.TryGetValue(f.Ticket.Key, out var was) || was.Kind != f.Kind || was.State != f.State)
            .Select(f => f.Ticket.TicketId)];
    }
}
