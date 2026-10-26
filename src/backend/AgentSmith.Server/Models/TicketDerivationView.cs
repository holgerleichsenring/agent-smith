namespace AgentSmith.Server.Models;

/// <summary>
/// 2026-10-26-bdd0f: one spec derivation of a ticket, apart from its attempts. Outcome is
/// <see cref="TicketDerivationOutcomes"/>'s word; Summary is the run's own sentence — where the specs are, why it
/// stopped or why it failed; PullRequests are the spec pull requests it opened or found.
/// </summary>
public sealed record TicketDerivationView(
    string RunId, string Status, DateTimeOffset StartedAt, DateTimeOffset? FinishedAt, decimal Cost,
    string Outcome, int SpecCount, IReadOnlyList<string> PullRequests, string? Summary);
