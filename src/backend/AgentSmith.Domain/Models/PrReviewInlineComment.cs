namespace AgentSmith.Domain.Models;

/// <summary>
/// One inline PR review comment, anchored to a new-file line span (inclusive,
/// new-side numbering — the same anchor convention as the observation
/// line_range). Body is the rendered markdown for the finding(s) grouped at
/// this anchor; Severity is the most severe grouped finding (enum name of the
/// observation severity), Category the leading finding's category tag.
/// </summary>
public sealed record PrReviewInlineComment(
    string File,
    int StartLine,
    int EndLine,
    string Severity,
    string? Category,
    string Body);
