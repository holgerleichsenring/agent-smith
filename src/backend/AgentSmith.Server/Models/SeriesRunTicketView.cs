using AgentSmith.Application.Services.Series;
namespace AgentSmith.Server.Models;

/// <summary>2026-11-23-31dcb: one member ticket on the series page.</summary>
/// <param name="Depth">The longest requires path to it inside the series; 0 without one.</param>
/// <param name="Requires">The ticket ids it requires, inside or outside the series.</param>
/// <param name="Repos">The repos it has pull requests in, in the project's repo order.</param>
/// <param name="Status">The newest attempt's status; null before any.</param>
public sealed record SeriesRunTicketView(
    string TicketId, string Key, string Title, bool FollowUp, int Depth, IReadOnlyList<string> Requires,
    IReadOnlyList<string> Repos, bool Landed, string? Status, IReadOnlyList<SeriesRunPullRequestView> PullRequests)
{
    /// <summary>2026-11-23-31dcg: the ticket's hold as its ticket page and the ticket list read it; null when nothing holds it.</summary>
    public AgentSmith.Application.Services.Holds.TicketHold? Hold { get; init; }

    /// <summary>2026-11-23-f562: the newest attempt's run id — the series Overview's Retry, See why and See run act on it;
    /// null before any attempt.</summary>
    public string? RunId { get; init; }

    /// <summary>2026-11-25-9992a: the ticket's one kind (<see cref="AgentSmith.Application.Services.Series.SeriesTicketKinds"/>),
    /// its words, its waiting pull requests' CI together, and whether a run is at work on it — the fact's, never the browser's.</summary>
    public string Kind { get; init; } = AgentSmith.Application.Services.Series.SeriesTicketKinds.Waiting;

    /// <inheritdoc cref="Kind"/>
    public string State { get; init; } = string.Empty;

    /// <inheritdoc cref="Kind"/>
    public string? Ci { get; init; }

    /// <inheritdoc cref="Kind"/>
    public bool Working { get; init; }

    /// <summary>2026-11-25-9992j: what every run of the ticket cost, from the fact — the page's ticket details show it.</summary>
    public decimal CostUsd { get; init; }

    /// <summary>2026-11-27-ecf2b: the repository the series page draws the ticket on — the first of its specs' repositories
    /// whose pull request carries code, else its first spec's; without specs the first pull request carrying code, else the
    /// first; null with neither.</summary>
    public string? LaneRepo { get; init; }

    /// <summary>2026-11-26-5201e: where the ticket opens in its tracker, as the ticket list reads it; null when it has none.</summary>
    public string? WebUrl { get; init; }

    /// <summary>2026-11-25-9992e: the series master's newest act on the ticket, from the fact; null when it never acted on it.</summary>
    public AgentSmith.Application.Services.Series.SeriesTicketLastAct? LastAct { get; init; }
}
