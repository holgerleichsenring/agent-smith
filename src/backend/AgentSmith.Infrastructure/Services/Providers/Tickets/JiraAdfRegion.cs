using System.Text.Json;
using System.Text.Json.Nodes;
using AgentSmith.Contracts.Tickets;

namespace AgentSmith.Infrastructure.Services.Providers.Tickets;

/// <summary>
/// 2026-10-28-7a69b: the framework's region of a Jira description, replaced inside the stored
/// document rather than through text.
/// <para>
/// A filing renders one paragraph per line, so each marker is a top-level paragraph of its own. The
/// nodes from the begin marker's paragraph to the end marker's are swapped for the new region's;
/// every other node goes back exactly as Jira returned it — mentions, links, tables and marks
/// included, which is what the text round trip through <see cref="JiraAdfParser"/> could not keep.
/// </para>
/// </summary>
internal static class JiraAdfRegion
{
    /// <summary>
    /// The document with <paramref name="region"/> in place, or null when it carries no complete
    /// marker pair and <paramref name="append"/> is false.
    /// </summary>
    public static JsonObject? Place(JsonObject? document, string region, bool append)
    {
        var doc = document ?? new JsonObject { ["type"] = "doc", ["version"] = 1 };
        if (doc["content"] is not JsonArray content) doc["content"] = content = [];
        var (begin, end) = Markers(content);
        // A marker moved into a panel or list, or left without its partner, is the framework's text somewhere.
        if (begin < 0 && (!append || content.Any(n => TextOf(n).Contains(FramedTicketRegion.BeginIdentifier, StringComparison.Ordinal)
            || TextOf(n).Contains(FramedTicketRegion.EndIdentifier, StringComparison.Ordinal)))) return null;
        var at = begin < 0 ? content.Count : begin;
        for (var i = end; i >= begin && begin >= 0; i--) content.RemoveAt(i);
        foreach (var node in Rendered(region)) content.Insert(at++, node);
        return doc;
    }

    // The first begin paragraph and the first end paragraph after it; (-1, -1) when the pair is not whole.
    private static (int Begin, int End) Markers(JsonArray content)
    {
        var begin = IndexOf(content, FramedTicketRegion.BeginIdentifier, 0);
        var end = begin < 0 ? -1 : IndexOf(content, FramedTicketRegion.EndIdentifier, begin + 1);
        return end < 0 ? (-1, -1) : (begin, end);
    }

    private static int IndexOf(JsonArray content, string identifier, int from)
    {
        for (var i = from; i < content.Count; i++)
            if (content[i]?["type"]?.GetValue<string>() == "paragraph"
                && TextOf(content[i]).TrimStart().StartsWith("<!--", StringComparison.Ordinal)
                && TextOf(content[i]).Contains(identifier, StringComparison.Ordinal))
                return i;
        return -1;
    }

    private static string TextOf(JsonNode? node) => node switch
    {
        JsonObject o when o["type"]?.GetValue<string>() == "text" => o["text"]?.GetValue<string>() ?? string.Empty,
        JsonObject o when o["content"] is JsonArray children => string.Concat(children.Select(TextOf)),
        _ => string.Empty,
    };

    private static IEnumerable<JsonNode> Rendered(string region) =>
        JsonSerializer.SerializeToNode(JiraAdfRenderer.FromMultilineText(region))!["content"]!.AsArray()
            .Select(node => node!.DeepClone()).ToList();
}
