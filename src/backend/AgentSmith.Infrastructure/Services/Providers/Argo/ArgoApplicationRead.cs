using System.Text.Json.Nodes;
using AgentSmith.Contracts.Providers;

namespace AgentSmith.Infrastructure.Services.Providers.Argo;

/// <summary>2026-11-23-c81ec: an Application's JSON as Argo CD's API answers it, read into its sync, health and last
/// operation; anything absent reads null.</summary>
public static class ArgoApplicationRead
{
    public static ArgoApplicationState From(JsonNode application)
    {
        ArgumentNullException.ThrowIfNull(application);
        var status = application["status"];
        return new ArgoApplicationState(
            Text(status?["sync"]?["status"]), Text(status?["health"]?["status"]), Text(status?["health"]?["message"]),
            Text(status?["operationState"]?["phase"]), Text(status?["operationState"]?["message"]));
    }

    private static string? Text(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<string>(out var text) && text.Length > 0 ? text : null;
}
