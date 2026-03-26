namespace AgentSmith.Contracts.Models;

/// <summary>
/// A secret found in git commit history.
/// </summary>
public sealed record HistoryFinding(
    string PatternId,
    string Severity,
    string CommitHash,
    string File,
    int Line,
    string Title,
    string Description,
    string? MatchedText,
    bool StillInWorkingTree,
    string? Provider = null,
    string? RevokeUrl = null);
