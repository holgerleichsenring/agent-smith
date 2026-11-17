namespace AgentSmith.Application.Services.TicketActs;

/// <summary>2026-11-17-e061a: one pull request a ticket's runs named; one whose record reads closed is not tried.</summary>
public sealed record TicketPullRequestToClose(string Url, string Repo, bool AlreadyClosed);
