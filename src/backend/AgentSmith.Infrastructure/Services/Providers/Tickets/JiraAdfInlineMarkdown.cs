using System.Text.Json;
using Node = AgentSmith.Infrastructure.Services.Providers.Tickets.JiraAdfNode;

namespace AgentSmith.Infrastructure.Services.Providers.Tickets;

/// <summary>
/// Writes the inline content of an ADF node as markdown: text with its marks, hard breaks, mentions, emoji and
/// inline cards. Text is written verbatim except '&lt;', written '\&lt;' — ADF text is never HTML, and every
/// other character is left alone so a marker or keyword in the body still matches.
/// </summary>
internal static class JiraAdfInlineMarkdown
{
    private const string HardBreak = "  \n";

    public static string Write(JsonElement node) => string.Concat(Node.Children(node).Select(Inline));

    /// <summary>The verbatim inline text of a node, for a code block.</summary>
    public static string Raw(JsonElement node) =>
        string.Concat(Node.Children(node).Select(c => Node.Type(c) == "hardBreak" ? "\n" : Node.Text(c) ?? Raw(c)));

    public static string Link(string text, string url) => $"[{text}]({url.Replace(" ", "%20").Replace(")", "%29")})";

    public static string Escaped(string text) => text.Replace("<", "\\<");

    public static bool IsInline(JsonElement node) =>
        Node.Type(node) is "text" or "hardBreak" or "mention" or "emoji" or "inlineCard" or "date" or "status";

    private static string Inline(JsonElement node) => Node.Type(node) switch
    {
        "text" => Marked(node, Node.Text(node) ?? string.Empty),
        "hardBreak" => HardBreak,
        "mention" => Escaped(Node.Attr(node, "text") ?? string.Empty),
        "emoji" => Escaped(Node.Attr(node, "text") ?? Node.Attr(node, "shortName") ?? string.Empty),
        "inlineCard" => Node.Attr(node, "url") is { } url ? Link(Escaped(url), url) : string.Empty,
        _ => Write(node),
    };

    private static string Marked(JsonElement node, string text)
    {
        var marks = Marks(node).ToList();
        var written = marks.Any(m => Node.Type(m) == "code") ? Wrapped(text, "`") : Escaped(text);
        if (marks.Any(m => Node.Type(m) == "em")) written = Wrapped(written, "*");
        if (marks.Any(m => Node.Type(m) == "strong")) written = Wrapped(written, "**");
        if (marks.Any(m => Node.Type(m) == "strike")) written = Wrapped(written, "~~");
        return marks.FirstOrDefault(m => Node.Type(m) == "link") is { ValueKind: JsonValueKind.Object } link
            && Node.Attr(link, "href") is { } href
            ? Link(written, href)
            : written;
    }

    private static IEnumerable<JsonElement> Marks(JsonElement node) =>
        node.TryGetProperty("marks", out var marks) && marks.ValueKind == JsonValueKind.Array ? marks.EnumerateArray() : [];

    // A delimiter beside whitespace does not open or close emphasis, so the whitespace goes outside it.
    private static string Wrapped(string text, string delimiter)
    {
        var core = text.Trim();
        if (core.Length == 0) return text;
        var lead = text[..(text.Length - text.TrimStart().Length)];
        var trail = text[text.TrimEnd().Length..];
        return lead + delimiter + core + delimiter + trail;
    }
}
