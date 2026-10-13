namespace AgentSmith.Server.Models;

/// <summary>
/// 2026-10-13-9a59a: a run of the ticket that is not an attempt — another pipeline (pr-review,
/// security-scan, init), or a code run cancelled before it started a step.
/// </summary>
/// <param name="NeverStarted">A code run that never started a step.</param>
public sealed record TicketOtherRunView(
    string RunId,
    string Pipeline,
    string Status,
    DateTimeOffset StartedAt,
    DateTimeOffset? FinishedAt,
    decimal Cost,
    bool NeverStarted);
