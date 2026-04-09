namespace AgentSmith.Contracts.Services;

/// <summary>
/// LLM-assessed status of a single finding from the convergence consolidation.
/// </summary>
public sealed record FindingAssessment(
    string File,
    int Line,
    string Title,
    string Status,   // confirmed | false_positive
    string Reason);
