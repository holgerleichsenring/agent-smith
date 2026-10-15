namespace AgentSmith.Contracts.Tickets;

/// <summary>
/// 2026-10-14-dbceg: a ticket landed — written once, keyed by the tracker connection and the ticket
/// key, and kept after every run of the ticket is deleted.
/// </summary>
/// <param name="Tracker">The tracker CONNECTION's catalog name.</param>
/// <param name="TicketKey">The ticket's key, <c>TicketKey.For(platform, id)</c>.</param>
/// <param name="TicketId">The ticket's id in the tracker's own spelling.</param>
/// <param name="Project">The project whose pull requests (or whose person) landed it.</param>
/// <param name="Kind">One of <see cref="TicketLandingKinds"/>.</param>
/// <param name="LandedAt">When the host says the last pull request ended, or when the person said so.</param>
/// <param name="By">The person who marked it by hand; null for a merge.</param>
/// <param name="Note">What the person wrote when marking it by hand.</param>
/// <param name="PrUrl">The newest merged pull request's URL; null by hand.</param>
public sealed record TicketLandingFact(
    string Tracker,
    string TicketKey,
    string TicketId,
    string Project,
    string Kind,
    DateTimeOffset LandedAt,
    string? By,
    string? Note,
    string? PrUrl);
