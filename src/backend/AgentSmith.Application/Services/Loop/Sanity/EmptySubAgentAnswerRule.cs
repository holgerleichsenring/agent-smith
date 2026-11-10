using System.Text.Json;

namespace AgentSmith.Application.Services.Loop.Sanity;

/// <summary>
/// A sub-agent that answered nothing: a spawn_agents row whose status is Empty.
/// </summary>
public sealed class EmptySubAgentAnswerRule : IToolResultSanityRule
{
    public string? Observe(ToolResultSanityCall call, ToolResultSanityScope scope) => call.Tool switch
    {
        "spawn_agents" => EmptyChildren(call.Result),
        _ => null,
    };

    private static string? EmptyChildren(string result)
    {
        using var document = Parse(result);
        if (document?.RootElement is not { ValueKind: JsonValueKind.Object } root
            || !root.TryGetProperty("results", out var rows) || rows.ValueKind != JsonValueKind.Array)
            return null;
        var empty = rows.EnumerateArray()
            .Where(row => row.ValueKind == JsonValueKind.Object && Text(row, "status") == nameof(SubAgentStatus.Empty))
            .Select(row => $"{Text(row, "name")} ({Text(row, "sub_agent_id")})")
            .ToList();
        return empty.Count == 0
            ? null
            : $"{string.Join(", ", empty)} answered nothing — an empty sub-agent proves nothing; "
              + "re-spawn it with a narrower task or do that part yourself before relying on it.";
    }

    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static JsonDocument? Parse(string result)
    {
        try { return JsonDocument.Parse(result); }
        catch (JsonException) { return null; }
    }
}
