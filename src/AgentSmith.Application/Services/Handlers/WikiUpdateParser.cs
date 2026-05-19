using System.Text.Json;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Parses wiki_updates from an LLM response. Returns an empty dictionary on
/// parse failure, but logs the failure so diagnosis is possible — the empty
/// result is only legitimate when the LLM explicitly returned no updates.
/// Fence stripping flows through <see cref="ITolerantJsonParser"/>.
/// </summary>
public sealed class WikiUpdateParser(ITolerantJsonParser tolerantParser)
{
    public Dictionary<string, string> Parse(string llmResponse, ILogger? logger = null)
    {
        var parsed = tolerantParser.ParseObject(llmResponse);
        if (parsed.Document is null)
        {
            logger?.LogWarning(
                "Failed to parse wiki updates JSON — returning empty dict. Length: {Length}",
                llmResponse.Length);
            return new Dictionary<string, string>();
        }
        using (parsed.Document)
        {
            try
            {
                var root = parsed.Document.RootElement;
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
                    "Failed to map wiki updates JSON to dict — returning empty dict. Length: {Length}",
                    llmResponse.Length);
                return new Dictionary<string, string>();
            }
        }
    }
}
