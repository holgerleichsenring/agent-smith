namespace AgentSmith.Server.Models;

/// <summary>2026-10-14-dbcen: a ticket a never-run ticket waits for, named by its tracker key.</summary>
/// <param name="Key">The name a person calls it by: a Jira id as it is, every other tracker's number behind a hash.</param>
/// <param name="Title">Its filed title where one is recorded, else <paramref name="Key"/>.</param>
/// <param name="Closed">2026-10-15-114fb: the required ticket is in the closed bucket; the dependent still waits.</param>
/// <param name="StartsOnMerge">2026-11-22-6d85d: its merge is one of those that unblock the dependent — every
/// prerequisite is green and only merges remain.</param>
public sealed record TicketWaitsForView(string TicketId, string? Project, string Key, string Title, bool Closed = false,
    bool StartsOnMerge = false);
