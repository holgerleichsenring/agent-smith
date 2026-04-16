namespace AgentSmith.Contracts.Models;

/// <summary>
/// A single typed observation from a skill agent during discussion.
/// Replaces free-text discussion entries with structured, machine-readable output.
/// ID is assigned by the framework, not the LLM.
/// </summary>
public sealed record SkillObservation(
    int Id,
    string Role,
    ObservationConcern Concern,
    string Description,
    string Suggestion,
    bool Blocking,
    ObservationSeverity Severity,
    int Confidence,
    string? Rationale = null,
    string? Location = null,
    ObservationEffort? Effort = null);
