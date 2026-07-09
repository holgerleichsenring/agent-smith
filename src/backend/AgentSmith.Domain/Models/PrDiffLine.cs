namespace AgentSmith.Domain.Models;

/// <summary>
/// One line inside a diff hunk. Added lines carry only a new line number,
/// removed lines only an old one, context lines both.
/// </summary>
public sealed record PrDiffLine(
    PrDiffLineKind Kind,
    int? OldLineNumber,
    int? NewLineNumber,
    string Content);
