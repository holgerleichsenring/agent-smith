using System.Text.Json;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Prompts;

/// <summary>
/// Parses JSON plan responses from the LLM into Plan domain objects.
/// Migrated from Infrastructure during the M.E.AI refactor (p0119a).
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
            var decisions = root.TryGetProperty("decisions", out var dArr)
                ? dArr.EnumerateArray().Select(ParseDecision).ToList()
                : new List<PlanDecision>();

            return new Plan(summary, steps, rawJson, decisions);
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

    private static PlanDecision ParseDecision(JsonElement element)
    {
        var category = element.TryGetProperty("category", out var cat)
            ? cat.GetString() ?? "Implementation"
            : "Implementation";
        var decision = element.GetProperty("decision").GetString() ?? "";
        return new PlanDecision(category, decision);
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
