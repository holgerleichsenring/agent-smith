using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Parses wiki_updates from an LLM response. Returns an empty dictionary on
/// parse failure, but logs the failure so diagnosis is possible — the empty
/// result is only legitimate when the LLM explicitly returned no updates.
/// </summary>
internal static class WikiUpdateParser
{
    public static Dictionary<string, string> Parse(string llmResponse, ILogger? logger = null)
    {
        try
        {
            var json = llmResponse.Trim();
            if (json.StartsWith("```"))
            {
                var firstNewline = json.IndexOf('\n');
                var lastFence = json.LastIndexOf("```", StringComparison.Ordinal);
                if (firstNewline > 0 && lastFence > firstNewline)
                    json = json[(firstNewline + 1)..lastFence].Trim();
            }

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("wiki_updates", out var updates))
            {
                logger?.LogWarning(
                    "Wiki response contained valid JSON but no 'wiki_updates' property — returning empty dict");
                return new Dictionary<string, string>();
            }

            var result = new Dictionary<string, string>();
            foreach (var prop in updates.EnumerateObject())
            {
                var content = prop.Value.GetString();
                if (!string.IsNullOrWhiteSpace(content))
                    result[prop.Name] = content;
            }

            return result;
        }
        catch (JsonException ex)
        {
            logger?.LogWarning(ex,
                "Failed to parse wiki updates JSON — returning empty dict. Response length: {Length}",
                llmResponse.Length);
            return new Dictionary<string, string>();
        }
    }
}
