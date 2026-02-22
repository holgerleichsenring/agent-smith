using System.Text.Json;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Domain.Models;

namespace AgentSmith.Infrastructure.Services.Providers.Agent;

/// <summary>
/// Shared plan parsing logic used by all AI agent providers (Claude, OpenAI, Gemini).
/// Parses JSON plan responses from LLMs into Plan domain objects.
/// </summary>
public static class PlanParser
{
    public static Plan Parse(string providerName, string rawJson)
    {
        try
        {
            var cleaned = StripMarkdownCodeBlock(rawJson);
            using var doc = JsonDocument.Parse(cleaned);
            var root = doc.RootElement;

            var summary = root.GetProperty("summary").GetString() ?? "";
            var steps = root.GetProperty("steps").EnumerateArray()
                .Select(ParseStep)
                .ToList();

            return new Plan(summary, steps, rawJson);
        }
        catch (JsonException ex)
        {
            throw new ProviderException(
                providerName,
                $"Failed to parse plan response from {providerName}: {ex.Message}",
                ex);
        }
    }

    private static string StripMarkdownCodeBlock(string text)
    {
        var trimmed = text.Trim();
        if (trimmed.StartsWith("```"))
        {
            var firstNewline = trimmed.IndexOf('\n');
            if (firstNewline >= 0)
                trimmed = trimmed[(firstNewline + 1)..];
        }
        if (trimmed.EndsWith("```"))
            trimmed = trimmed[..^3].TrimEnd();
        return trimmed;
    }

    private static PlanStep ParseStep(JsonElement element)
    {
        var order = element.GetProperty("order").GetInt32();
        var description = element.GetProperty("description").GetString() ?? "";
        var targetFile = element.TryGetProperty("target_file", out var tf)
            ? new FilePath(tf.GetString()!)
            : null;
        var changeType = element.TryGetProperty("change_type", out var ct)
            ? ct.GetString() ?? "Modify"
            : "Modify";

        return new PlanStep(order, description, targetFile, changeType);
    }
}
