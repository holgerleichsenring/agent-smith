using System.Text.Json;

namespace AgentSmith.Application.Services.Handlers;

internal static class WikiUpdateParser
{
    public static Dictionary<string, string> Parse(string llmResponse)
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
                return new Dictionary<string, string>();

            var result = new Dictionary<string, string>();
            foreach (var prop in updates.EnumerateObject())
            {
                var content = prop.Value.GetString();
                if (!string.IsNullOrWhiteSpace(content))
                    result[prop.Name] = content;
            }

            return result;
        }
        catch (JsonException)
        {
            return new Dictionary<string, string>();
        }
    }
}
