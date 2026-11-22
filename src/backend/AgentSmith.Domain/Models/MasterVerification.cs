namespace AgentSmith.Domain.Models;

/// <summary>
/// p0241: what a spec's coding master left the pipeline. 2026-11-22-96c8a: no closing tool call any more — the report
/// is the master's last words (the final message of a pass that ended on its own), shown as written, and the
/// follow-ups and refused instructions are read off it by one structured read. Nothing here decides delivery: the
/// verify stages do, and a report must exist. The record keeps its p0241 name and pipeline key.
/// </summary>
/// <param name="Report">The master's markdown report; null when it ended without one.</param>
/// <param name="FollowUps">The follow-up specs the report proposes; null when the report was not read.</param>
/// <param name="IgnoredInstructions">p0316: the ticket instructions the report says were refused.</param>
/// <param name="ReadProblem">Why the report could not be read, or null when it was read (or there was none).</param>
public sealed record MasterVerification(
    string? Report,
    IReadOnlyList<MasterFollowUp>? FollowUps = null,
    IReadOnlyList<IgnoredInstruction>? IgnoredInstructions = null,
    string? ReadProblem = null);

/// <summary>
/// p0316: a ticket-embedded instruction the master REFUSED to follow — either
/// out-of-scope/destructive (the never-comply catalog) or a prompt-injection
/// attempt ("ignore previous instructions"). The framework persists these as
/// auditable data (run event + result.md section): the agent reports verbatim
/// quote + reason, the framework surfaces them. Absent/empty = the master ignored nothing.
/// </summary>
public sealed record IgnoredInstruction(string Quote, string Reason);
