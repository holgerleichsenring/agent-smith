using AgentSmith.Application.Services.Holds;
using AgentSmith.Application.Services.Series;
using AgentSmith.Contracts.Series;

namespace AgentSmith.Tests.Series;

/// <summary>2026-11-17-0207a: series records and facts for the series tests.</summary>
internal static class SeriesRecords
{
    internal static readonly DateTimeOffset T = new(2026, 11, 17, 18, 37, 0, TimeSpan.Zero);

    internal static SeriesTicket Ticket(string id, string title = "work") => new(id, $"SCRUM-{id}", title, false, T);

    internal static SeriesRunRecord Running(string id = "sr-1", string conversation = "conv-1", bool mayStartRuns = true) =>
        new(id, conversation, "sample", "operator", mayStartRuns, SeriesRunStates.Running,
            [Ticket("43"), Ticket("44"), Ticket("45")], T);

    internal static SeriesTicketFact Fact(string id, string? status = null, bool finished = true, DateTimeOffset? landed = null,
        params string[] requires) =>
        new(Ticket(id)) { LatestStatus = status, LatestFinished = finished, LandedAt = landed, Requires = requires,
            LatestRunId = status is null ? null : $"run-{id}", FirstStartedAt = status is null ? null : T, CostUsd = status is null ? 0 : 1.5m };

    /// <summary>2026-11-25-9992a: the fact held as the ticket list reads it.</summary>
    internal static SeriesTicketFact Held(this SeriesTicketFact fact, string addressee, string @case, DateTimeOffset? since = null,
        params string[] names) => fact with { Hold = new TicketHold(addressee, @case, null, since) { Names = names } };

    /// <summary>2026-11-25-9992a: one pull request of the fact, in a state, with its CI on its head.</summary>
    internal static SeriesTicketFact WithPr(this SeriesTicketFact fact, string state, string? ci, string? failure = null) =>
        fact with { PullRequests = [.. fact.PullRequests, new SeriesRunPullRequestView("api", $"{fact.PullRequests.Count + 1}",
            $"https://host/pr/{fact.Ticket.TicketId}/{fact.PullRequests.Count + 1}", state, ci, failure, null, "main")] };
}
