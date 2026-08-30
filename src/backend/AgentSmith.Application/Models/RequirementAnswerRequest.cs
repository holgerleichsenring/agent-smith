namespace AgentSmith.Application.Models;

/// <summary>
/// 2026-08-30-3c12: one answer exactly as the model stated it, before any of it is
/// checked. Every field is the raw argument of a single tool call — the carrier is one
/// entry per call, so a worker corrects one row without re-emitting the rest.
/// </summary>
public sealed record RequirementAnswerRequest(
    string Group,
    string Station,
    string RequirementId,
    string Operation,
    string Verdict,
    string Scope,
    string File,
    int StartLine,
    string CoversMembers,
    string MissingInput);
