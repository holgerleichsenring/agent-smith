namespace AgentSmith.Contracts.Tickets;

/// <summary>
/// p0328: stable anchors for expectation ticket comments, mirroring
/// <see cref="OpenQuestionsCommentMarkers"/>. The leading marker lets a future
/// tracker-reply hook (p0328b) recognise which comment a reply ratifies;
/// markdown platforms use an invisible HTML comment, Jira a visible bracket
/// tag.
/// </summary>
public static class ExpectationCommentMarkers
{
    public const string MarkdownLeadingMarker = "<!--agent-smith:expectation-->";
    public const string PlainTextLeadingMarker = "[agent-smith expectation]";

    public static bool IsExpectationComment(string body) =>
        body.Contains(MarkdownLeadingMarker, StringComparison.Ordinal)
        || body.Contains(PlainTextLeadingMarker, StringComparison.Ordinal);
}
