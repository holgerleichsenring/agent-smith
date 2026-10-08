using System.Text.Json;

namespace AgentSmith.Server.Services.Webhooks;

/// <summary>
/// Determines the source platform and event type from webhook
/// request path, headers, and body content.
/// </summary>
internal static class WebhookPlatformDetector
{
    public static (string? Platform, string? EventType) Detect(
        string path, string body, IDictionary<string, string> headers)
    {
        return path switch
        {
            "/webhook/jira" => ("jira", ExtractJiraEventType(body)),
            "/webhook/github" => DetectFromHeaders(headers),
            "/webhook/gitlab" => DetectFromHeaders(headers),
            _ => DetectFromHeaders(headers) is { Platform: not null } fromHeaders
                ? fromHeaders
                : DetectAzureDevOpsFromBody(body),
        };
    }

    // 2026-10-08-e8b9a: Azure DevOps service hooks send no platform header unless the
    // subscription lists one, so on the shared /webhook route the body decides: Microsoft's
    // envelope carries a publisherId from a fixed set and a string eventType.
    private static readonly HashSet<string> AzureDevOpsPublishers =
        new(StringComparer.Ordinal) { "tfs", "rm", "pipelines", "distributedtask", "advsec" };

    private static (string? Platform, string? EventType) DetectAzureDevOpsFromBody(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            if (root.ValueKind == JsonValueKind.Object
                && root.TryGetProperty("publisherId", out var publisher)
                && publisher.ValueKind == JsonValueKind.String
                && AzureDevOpsPublishers.Contains(publisher.GetString()!)
                && root.TryGetProperty("eventType", out var evt)
                && evt.ValueKind == JsonValueKind.String)
                return ("azuredevops", evt.GetString());
        }
        catch (JsonException) { /* not JSON — no platform */ }
        return (null, null);
    }

    private static (string? Platform, string? EventType) DetectFromHeaders(
        IDictionary<string, string> headers)
    {
        if (headers.TryGetValue("X-GitHub-Event", out var ghEvent))
            return ("github", ghEvent);
        if (headers.TryGetValue("X-Gitlab-Event", out var glEvent))
            return ("gitlab", glEvent.Contains("Merge Request") ? "merge_request" : glEvent.ToLowerInvariant());
        if (headers.TryGetValue("X-Azure-DevOps-EventType", out var azdoEvent))
            return ("azuredevops", azdoEvent);
        return (null, null);
    }

    private static string? ExtractJiraEventType(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("webhookEvent", out var evt))
            {
                var value = evt.GetString() ?? "";
                return value.StartsWith("jira:", StringComparison.OrdinalIgnoreCase)
                    ? value["jira:".Length..]
                    : value;
            }
        }
        catch { /* payload parse failure handled downstream */ }
        return null;
    }
}
