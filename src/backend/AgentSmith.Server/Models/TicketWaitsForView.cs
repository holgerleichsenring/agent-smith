namespace AgentSmith.Server.Models;

/// <summary>2026-10-14-dbcen: a ticket a never-run ticket waits for, named by its tracker key.</summary>
/// <param name="Key">The name a person calls it by: a Jira id as it is, every other tracker's number behind a hash.</param>
/// <param name="Title">Its filed title where one is recorded, else <paramref name="Key"/>.</param>
public sealed record TicketWaitsForView(string TicketId, string? Project, string Key, string Title);
