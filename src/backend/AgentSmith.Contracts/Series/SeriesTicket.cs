namespace AgentSmith.Contracts.Series;

/// <summary>2026-11-17-0207a: one member ticket of a series run — what the series owns. Its requires edges, specs and
/// done criteria are read from the stores that keep them, never copied here.</summary>
/// <param name="TicketId">The tracker-native id runs are found by.</param>
/// <param name="Key">What a person calls it (the Jira key, the number behind a hash).</param>
/// <param name="Title">The ticket's title, the 'what' column.</param>
/// <param name="FollowUp">True for a ticket the series' own verify filed.</param>
/// <param name="AddedAt">When the series took it on.</param>
public sealed record SeriesTicket(string TicketId, string Key, string Title, bool FollowUp, DateTimeOffset AddedAt);
