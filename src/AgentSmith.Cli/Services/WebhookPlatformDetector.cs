using System.Text.Json;

namespace AgentSmith.Cli.Services;

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
            _ => DetectFromHeaders(headers),
        };
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
