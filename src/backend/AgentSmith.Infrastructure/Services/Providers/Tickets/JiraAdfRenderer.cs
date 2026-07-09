using System.Net;
using System.Text.RegularExpressions;

namespace AgentSmith.Infrastructure.Services.Providers.Tickets;

/// <summary>
/// p0147f: builds the Atlassian Document Format (ADF) envelope a Jira
/// REST v3 endpoint expects for body content (e.g. <c>POST /comment</c>).
/// </summary>
/// <remarks>
/// ADF is the only sane way to post a comment to Jira Cloud — the legacy
/// wiki-markup body field is rejected, and HTML is NOT rendered (it shows as
/// literal text). agent-smith's status/error comments are authored in a small
/// HTML subset (<c>&lt;b&gt;</c>, <c>&lt;br/&gt;</c>, HTML-encoded text) for the
/// GitHub/ADO providers; <see cref="FromCommentText"/> converts that subset into
/// real ADF (strong marks + hard breaks + decoded entities) so Jira renders it
/// like the others. <see cref="JiraAdfParser"/> reverses ADF for incoming
/// descriptions; this class is the outbound counterpart.
/// </remarks>
internal static partial class JiraAdfRenderer
{
    /// <summary>Wraps verbatim text as an ADF doc with a single text node.</summary>
    public static object FromPlainText(string text) =>
        Doc(new object[] { new { type = "text", text = EmptyToSpace(text) } });

    /// <summary>
    /// Converts agent-smith's HTML-subset comment text into ADF: <c>&lt;b&gt;…&lt;/b&gt;</c>
    /// → a <c>strong</c>-marked text node, <c>&lt;br/&gt;</c> → a hard break, and HTML
    /// entities (<c>&amp;#39;</c>, <c>&amp;lt;</c>, …) decoded. Structural tags are
    /// tokenised BEFORE entity decoding so an error message containing a literal
    /// <c>&lt;</c> (encoded as <c>&amp;lt;</c>) is never mistaken for a tag.
    /// </summary>
    public static object FromCommentText(string text)
    {
        var nodes = new List<object>();
        var bold = false;

        foreach (var token in TagSplitter().Split(text ?? string.Empty))
        {
            if (string.IsNullOrEmpty(token)) continue;
            if (LineBreakTag().IsMatch(token)) { nodes.Add(new { type = "hardBreak" }); continue; }
            if (BoldOpenTag().IsMatch(token)) { bold = true; continue; }
            if (BoldCloseTag().IsMatch(token)) { bold = false; continue; }

            var decoded = WebUtility.HtmlDecode(token);
            if (decoded.Length == 0) continue;
            nodes.Add(bold
                ? new { type = "text", text = decoded, marks = new[] { new { type = "strong" } } }
                : (object)new { type = "text", text = decoded });
        }

        if (nodes.Count == 0) nodes.Add(new { type = "text", text = " " });
        return Doc(nodes.ToArray());
    }

    /// <summary>Builds the full comment request body for <c>POST /comment</c>.</summary>
    public static object CommentBody(string text) => new { body = FromCommentText(text) };

    /// <summary>
    /// Renders multi-line text as one ADF paragraph PER LINE (empty line →
    /// empty paragraph). This is the exact inverse of
    /// <see cref="JiraAdfParser.ExtractText"/>, which appends one newline per
    /// paragraph — so a created description (e.g. a fenced yaml block) reads
    /// back with its line structure intact.
    /// </summary>
    public static object FromMultilineText(string text)
    {
        var paragraphs = (text ?? string.Empty).Replace("\r\n", "\n").Split('\n')
            .Select(line => line.Length == 0
                ? new { type = "paragraph", content = Array.Empty<object>() }
                : new
                {
                    type = "paragraph",
                    content = new object[] { new { type = "text", text = line } },
                })
            .Cast<object>()
            .ToArray();
        return new { type = "doc", version = 1, content = paragraphs };
    }

    private static object Doc(object[] inlineContent) => new
    {
        type = "doc",
        version = 1,
        content = new[] { new { type = "paragraph", content = inlineContent } }
    };

    // ADF rejects an empty text node — substitute a single space for an empty body.
    private static string EmptyToSpace(string text) => string.IsNullOrEmpty(text) ? " " : text;

    // Split on (and keep) the structural tags agent-smith emits.
    [GeneratedRegex(@"(</?b>|<br\s*/?>)", RegexOptions.IgnoreCase)]
    private static partial Regex TagSplitter();

    [GeneratedRegex(@"^<br\s*/?>$", RegexOptions.IgnoreCase)]
    private static partial Regex LineBreakTag();

    [GeneratedRegex(@"^<b>$", RegexOptions.IgnoreCase)]
    private static partial Regex BoldOpenTag();

    [GeneratedRegex(@"^</b>$", RegexOptions.IgnoreCase)]
    private static partial Regex BoldCloseTag();
}
