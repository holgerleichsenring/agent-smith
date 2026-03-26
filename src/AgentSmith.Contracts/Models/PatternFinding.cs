namespace AgentSmith.Contracts.Models;

/// <summary>
/// A finding from static pattern scanning (regex-based).
/// </summary>
public sealed record PatternFinding(
    string PatternId,
    string Category,
    string Severity,
    int Confidence,
    string File,
    int Line,
    string Title,
    string Description,
    string? Cwe,
    string? MatchedText,
    string? Provider = null,
    string? RevokeUrl = null);
