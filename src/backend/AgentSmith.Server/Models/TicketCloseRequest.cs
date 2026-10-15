namespace AgentSmith.Server.Models;

/// <summary>
/// 2026-10-15-114fa: a person closes a ticket — an optional note, and whether its pull requests and its tracker
/// ticket are closed too. Nothing chosen hides it only.
/// </summary>
public sealed record TicketCloseRequest(string? Note, bool ClosePullRequests, bool CloseOnTracker);
