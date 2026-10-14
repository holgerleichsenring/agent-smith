namespace AgentSmith.Domain.Models;

/// <summary>
/// The format a tracker text was written in, named by whoever read it from its source: the reader renders
/// html and markdown as formatted text and keeps plain text as it is.
/// </summary>
public static class TicketTextFormat
{
    public const string Html = "html";
    public const string Markdown = "markdown";
    public const string Text = "text";
}
