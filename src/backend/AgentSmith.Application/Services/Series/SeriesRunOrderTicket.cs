namespace AgentSmith.Application.Services.Series;

/// <summary>2026-11-23-31dcb: one member ticket as the series' order reads it — its requires edges (ticket ids, inside or
/// outside the series), whether it has an open pull request, and whether one of them is known to target a branch other
/// than its repo's default.</summary>
public sealed record SeriesRunOrderTicket(string TicketId, string Key, IReadOnlyList<string> Requires)
{
    public bool HasOpenPullRequest { get; init; }

    /// <summary>An open pull request's target is known and is not its repo's default branch — a retarget is still to come.
    /// False while the target is unknown.</summary>
    public bool TargetsElsewhere { get; init; }
}
