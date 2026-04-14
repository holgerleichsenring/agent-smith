using System.Text;
using System.Text.Json;

namespace AgentSmith.Infrastructure.Services.Providers.Tickets;

/// <summary>
/// Parses Atlassian Document Format (ADF) JSON into plain text.
/// Recursively walks the node tree extracting text content.
/// </summary>
internal static class JiraAdfParser
{
    public static string ExtractText(JsonElement element)
    {
        var builder = new StringBuilder();
        CollectTextNodes(element, builder);
        return builder.ToString().Trim();
    }

    private static void CollectTextNodes(JsonElement element, StringBuilder builder)
    {
        if (element.ValueKind != JsonValueKind.Object)
            return;

        if (element.TryGetProperty("type", out var typeEl)
            && typeEl.GetString() == "text"
            && element.TryGetProperty("text", out var textEl))
        {
            builder.Append(textEl.GetString());
        }

        if (element.TryGetProperty("content", out var contentEl)
            && contentEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in contentEl.EnumerateArray())
                CollectTextNodes(child, builder);

            if (typeEl.ValueKind == JsonValueKind.String)
            {
                var nodeType = typeEl.GetString();
                if (nodeType is "paragraph" or "heading" or "blockquote"
                    or "codeBlock" or "bulletList" or "orderedList" or "listItem")
                    builder.AppendLine();
            }
        }
    }
}
