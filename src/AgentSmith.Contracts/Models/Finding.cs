namespace AgentSmith.Contracts.Services;

/// <summary>
/// A single finding from a security scan or analysis.
/// </summary>
public sealed record Finding(
    string Severity,
    string File,
    int StartLine,
    int? EndLine,
    string Title,
    string Description,
    int Confidence,
    string ReviewStatus = "not_reviewed");  // confirmed | false_positive | not_reviewed
