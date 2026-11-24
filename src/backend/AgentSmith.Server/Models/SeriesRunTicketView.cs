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

    /// <summary>2026-11-23-31dcg: whether the series' held count counts it — a hold names someone and is not the merge that
    /// comes next.</summary>
    public static bool Held(AgentSmith.Application.Services.Holds.TicketHold? hold) =>
        hold is not null && hold.Addressee != AgentSmith.Application.Services.Holds.TicketHoldAddressees.Nobody
        && hold.Case != AgentSmith.Application.Services.Holds.TicketHoldCases.MergeNext;
}
