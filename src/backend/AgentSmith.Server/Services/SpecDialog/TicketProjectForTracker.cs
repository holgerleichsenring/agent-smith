using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// 2026-09-27-481bb: which projects ONE NAMED TRACKER'S copy of a ticket names.
/// <para>
/// The sweep beside this one asks every tracker and returns on the first that answers, which is
/// right when only a number is known. It is wrong once a person has PICKED a hit from a search:
/// the hit came from a particular board, and resolving it by sweeping again could answer for a
/// different one that happens to hold the same number. So the tracker is given, not guessed.
/// </para>
/// <para>
/// It narrows the catalogue rather than taking a second code path: the narrowing, the label match
/// and the report of projects matched elsewhere are the choice's, and a second implementation of
/// them would be a second answer to one question.
/// </para>
/// </summary>
public sealed class TicketProjectForTracker(TicketProjectChoice choice)
{
    /// <summary>Null when no such tracker is configured, or when it does not have the ticket.</summary>
    public async Task<TicketProjectAnswer?> ForTrackerAsync(
        AgentSmithConfig config, string tracker, string ticketId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(config);
        var connection = config.Trackers.Values
            .FirstOrDefault(t => string.Equals(t.Name, tracker, StringComparison.Ordinal));
        if (connection is null) return null;
        var scoped = new AgentSmithConfig
        {
            Projects = config.Projects,
            Trackers = new Dictionary<string, TrackerConnection> { [tracker] = connection },
        };
        return (await choice.LookupAsync(scoped, ticketId, ct)).Answer;
    }
}
