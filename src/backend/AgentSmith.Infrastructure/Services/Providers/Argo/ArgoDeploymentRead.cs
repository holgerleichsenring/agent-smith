using System.Text.Json.Nodes;
using AgentSmith.Contracts.Providers;

namespace AgentSmith.Infrastructure.Services.Providers.Argo;

/// <summary>
/// 2026-11-23-c81ec: the live Deployment manifest Argo CD's resource endpoint answers (a JSON string under 'manifest'),
/// read into what a rollout follow compares — the pod template's 'app' container image and its variables-checksum
/// annotation, spec.replicas, the generations and the ready and updated counts.
/// </summary>
public static class ArgoDeploymentRead
{
    public const string Container = "app";
    public const string ChecksumAnnotation = "agent-smith.io/variables-checksum";

    public static ArgoLiveDeployment? From(JsonNode answer)
    {
        ArgumentNullException.ThrowIfNull(answer);
        if (answer["manifest"] is not JsonValue manifest || !manifest.TryGetValue<string>(out var text)
            || JsonNode.Parse(text) is not { } deployment)
            return null;
        var template = deployment["spec"]?["template"];
        return new ArgoLiveDeployment(
            Image(template?["spec"]?["containers"] as JsonArray),
            Text(template?["metadata"]?["annotations"]?[ChecksumAnnotation]),
            Int(deployment["spec"]?["replicas"]),
            Long(deployment["metadata"]?["generation"]) ?? 0, Long(deployment["status"]?["observedGeneration"]) ?? 0,
            Int(deployment["status"]?["readyReplicas"]) ?? 0, Int(deployment["status"]?["updatedReplicas"]) ?? 0);
    }

    private static string? Image(JsonArray? containers) =>
        containers?.FirstOrDefault(c => Text(c?["name"]) == Container) is { } app ? Text(app["image"]) : null;

    private static string? Text(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

    private static long? Long(JsonNode? node) => node is JsonValue value && value.TryGetValue<long>(out var n) ? n : null;

    private static int? Int(JsonNode? node) => node is JsonValue value && value.TryGetValue<int>(out var n) ? n : null;
}
