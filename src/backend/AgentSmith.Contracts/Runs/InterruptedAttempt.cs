namespace AgentSmith.Contracts.Runs;

/// <summary>2026-11-22-6d85c: a ticket's newest code attempt that a lost process ended, owed one unchanged retry.</summary>
public sealed record InterruptedAttempt(string Project, string TicketId, string RunId);
