namespace AgentSmith.Contracts.Tickets;

/// <summary>
/// Stable anchor strings the comment template emits and PlanAnswerParser
/// detects. The markdown variant uses HTML comments (invisible in renderers
/// that strip them); the plain-text variant uses visible bracket anchors so
/// Jira-style endpoints that don't preserve HTML comments still round-trip.
/// </summary>
public static class OpenQuestionsCommentMarkers
{
    /// <summary>HTML-comment leading marker for markdown bodies (GitHub/GitLab/AzDO).</summary>
    public const string MarkdownLeadingMarker = "<!--agent-smith:open-questions-->";

    /// <summary>Visible leading marker for plain-text bodies (Jira).</summary>
    public const string PlainTextLeadingMarker = "[agent-smith open questions]";

    /// <summary>HTML-comment per-question anchor pattern: <c>&lt;!--Q{id}--&gt;</c>.</summary>
    public static string MarkdownQuestionAnchor(string id) => $"<!--Q{id}-->";

    /// <summary>Plain-text per-question anchor pattern: <c>[Q{id}]</c>.</summary>
    public static string PlainTextQuestionAnchor(string id) => $"[Q{id}]";

    /// <summary>Detects whether a comment body identifies as an agent-smith open-questions comment.</summary>
    public static bool IsOpenQuestionsComment(string commentBody)
        => commentBody.Contains(MarkdownLeadingMarker, StringComparison.Ordinal)
        || commentBody.Contains(PlainTextLeadingMarker, StringComparison.Ordinal);
}
