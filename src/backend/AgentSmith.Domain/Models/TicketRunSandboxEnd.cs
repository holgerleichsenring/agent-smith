namespace AgentSmith.Domain.Models;

/// <summary>
/// The last sandbox of a run that ended other than by a release — vanished, killed for memory,
/// exited, gone or unobserved — with the exit code and OOM flag a backend read, null without evidence.
/// </summary>
public sealed record TicketRunSandboxEnd(string Reason, int? ExitCode, bool? OomKilled);
