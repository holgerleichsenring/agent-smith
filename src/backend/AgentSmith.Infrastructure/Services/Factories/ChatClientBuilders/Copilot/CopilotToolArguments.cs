using System.Text.Json;

namespace AgentSmith.Infrastructure.Services.Factories.ChatClientBuilders.Copilot;

/// <summary>
/// 2026-09-23-4722a: the arguments a pending tool call carries, as the tool loop expects them.
/// The runtime hands them over as JSON; FunctionInvokingChatClient binds from a name/value map.
/// </summary>
internal static class CopilotToolArguments
{
    internal static IDictionary<string, object?> Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new Dictionary<string, object?>();
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return new Dictionary<string, object?>();
            return document.RootElement.EnumerateObject()
                .ToDictionary(p => p.Name, p => (object?)p.Value.Clone());
        }
        catch (JsonException)
        {
            // Malformed arguments are the model's mistake, not ours: an empty map lets the tool
            // refuse on its own terms rather than failing the whole turn here.
            return new Dictionary<string, object?>();
        }
    }
}
