using System.Text.Json;

namespace AgentSmith.Contracts.Commands;

/// <summary>
/// Reads <see cref="ContextKeys.RequestedByName"/> off a run's initial context, in both the
/// shape it is seeded in (<c>bool</c>) and the shape it comes back in from a JSON round-trip
/// through the capacity queue (<see cref="JsonElement"/>).
/// </summary>
public static class RequestedByNameExtensions
{
    public static bool IsRequestedByName(this IReadOnlyDictionary<string, object>? context)
    {
        if (context is null || !context.TryGetValue(ContextKeys.RequestedByName, out var value))
            return false;
        return value is true
            || value is JsonElement { ValueKind: JsonValueKind.True };
    }
}
