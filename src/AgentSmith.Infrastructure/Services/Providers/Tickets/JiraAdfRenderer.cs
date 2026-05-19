namespace AgentSmith.Infrastructure.Services.Providers.Tickets;

/// <summary>
/// p0147f: builds the Atlassian Document Format (ADF) envelope a Jira
/// REST v3 endpoint expects for body content (e.g. <c>POST /comment</c>).
/// Plain comment text is wrapped into a doc -> paragraph -> text tree.
/// </summary>
/// <remarks>
/// ADF is the only sane way to post a comment to Jira Cloud — the legacy
/// wiki-markup body field is rejected. <see cref="JiraAdfParser"/> reverses
/// this for incoming descriptions; this class is the outbound counterpart.
/// </remarks>
internal static class JiraAdfRenderer
{
    /// <summary>
    /// Wraps plain comment text as an ADF doc with a single paragraph.
    /// Returned object is anonymous so it serialises straight to JSON
    /// via System.Text.Json without an intermediate DTO.
    /// </summary>
    public static object FromPlainText(string text) => new
    {
        type = "doc",
        version = 1,
        content = new[]
        {
            new
            {
                type = "paragraph",
                content = new[]
                {
                    new { type = "text", text }
                }
            }
        }
    };

    /// <summary>
    /// Convenience: builds the full comment request body
    /// (<c>{ "body": &lt;adf-doc&gt; }</c>) for <c>POST /comment</c>.
    /// </summary>
    public static object CommentBody(string text) => new { body = FromPlainText(text) };
}
