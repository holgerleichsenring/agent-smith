using System.Text.Json;
using Inline = AgentSmith.Infrastructure.Services.Providers.Tickets.JiraAdfInlineMarkdown;
using Node = AgentSmith.Infrastructure.Services.Providers.Tickets.JiraAdfNode;

namespace AgentSmith.Infrastructure.Services.Providers.Tickets;

/// <summary>
/// Writes an ADF document as markdown, so a Jira body keeps its paragraphs, headings, lists, task lists, code
/// blocks, quotes, rules and cards, and one renderer serves Jira beside the trackers that store markdown. Our own
/// comments, posted as ADF text that already holds markdown, read back as that markdown. Inline content is
/// <see cref="JiraAdfInlineMarkdown"/>'s; a node it does not know is read through its content.
/// </summary>
internal static class JiraAdfMarkdown
{
    private const string BlockGap = "\n\n";

    public static string Write(JsonElement doc) => Blocks(doc).Trim();

    private static string Blocks(JsonElement node) =>
        string.Join(BlockGap, Node.Children(node).Select(Block).Where(b => b.Length > 0));

    private static string Block(JsonElement node) => Node.Type(node) switch
    {
        "paragraph" => Inline.Write(node),
        "heading" => new string('#', Math.Clamp(Node.IntAttr(node, "level") ?? 1, 1, 6)) + " " + Inline.Write(node),
        "bulletList" => List(node, _ => "- "),
        "orderedList" => List(node, i => $"{(Node.IntAttr(node, "order") ?? 1) + i}. "),
        "taskList" => List(node, _ => "- "),
        "codeBlock" => $"```{Node.Attr(node, "language")}\n{Inline.Raw(node)}\n```",
        "blockquote" => Prefixed(Blocks(node), "> "),
        "rule" => "---",
        "blockCard" or "embedCard" => Node.Attr(node, "url") is { } url ? Inline.Link(Inline.Escaped(url), url) : string.Empty,
        _ when Inline.IsInline(node) || Node.Children(node).Any(Inline.IsInline) => Inline.Write(Wrap(node)),
        _ => Blocks(node),
    };

    private static string List(JsonElement list, Func<int, string> marker) =>
        string.Join("\n", Node.Children(list).Select((item, i) => Item(item, marker(i))));

    private static string Item(JsonElement item, string marker) => Node.Type(item) switch
    {
        "taskItem" => Indented(marker + (Node.Attr(item, "state") == "DONE" ? "[x] " : "[ ] "), Inline.Write(item)),
        "listItem" => Indented(marker, string.Join("\n", Node.Children(item).Select(Block).Where(b => b.Length > 0))),
        _ => Prefixed(Block(item), "  "),
    };

    // A list item's continuation lines line up under its first character.
    private static string Indented(string marker, string body) =>
        marker + body.Replace("\n", "\n" + new string(' ', marker.Length));

    private static string Prefixed(string text, string prefix) =>
        string.Join("\n", text.Split('\n').Select(line => line.Length == 0 ? prefix.TrimEnd() : prefix + line));

    // An inline node met where a block was expected is written as the only content of a paragraph.
    private static JsonElement Wrap(JsonElement node) =>
        Inline.IsInline(node)
            ? JsonSerializer.SerializeToElement(new { type = "paragraph", content = new[] { node } })
            : node;
}
