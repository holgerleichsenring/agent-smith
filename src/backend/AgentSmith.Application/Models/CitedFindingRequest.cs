namespace AgentSmith.Application.Models;

/// <summary>
/// 2026-08-30-03e1: one cited finding exactly as the model stated it, before any of it is
/// checked. Every field is the raw argument of a single tool call — the carrier is one
/// finding per call, so a worker corrects one row without re-emitting the rest.
/// </summary>
public sealed record CitedFindingRequest(
    string Group,
    string Station,
    string RequirementId,
    string Detail,
    string Scope,
    string File,
    int StartLine,
    string CoversMembers);
