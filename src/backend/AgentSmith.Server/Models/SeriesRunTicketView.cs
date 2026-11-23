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

    /// <summary>2026-11-23-31dcg: What holds the rest lists it (<see cref="Held(AgentSmith.Application.Services.Holds.TicketHold?)"/>),
    /// sent so the page lists what the held count counts.</summary>
    public bool IsHeld => Held(Hold);

    /// <summary>2026-11-23-31dcg: whether What holds the rest lists it as held — a hold names someone and is not the merge
    /// that comes next.</summary>
    public static bool Held(AgentSmith.Application.Services.Holds.TicketHold? hold) =>
        hold is not null && hold.Addressee != AgentSmith.Application.Services.Holds.TicketHoldAddressees.Nobody
        && hold.Case != AgentSmith.Application.Services.Holds.TicketHoldCases.MergeNext;
}
