using System.Text.Json;

namespace AgentSmith.Infrastructure.Services.Providers.Tickets;

/// <summary>
/// 2026-10-28-7a69a: which Jira project an issue belongs to, read off its key.
/// <para>
/// <c>GET /issue/{key}</c> answers for the whole SITE, so two trackers on one site — one per Jira
/// project — would each return the other's issues, and a ticket named without a project would bind
/// to whichever was asked first. The key Jira RETURNS is read rather than the one asked for, so an
/// issue moved into this project from another still resolves here.
/// </para>
/// </summary>
internal static class JiraIssueKey
{
    /// <summary>Whether the issue is in <paramref name="projectKey"/>; a tracker with none holds every project.</summary>
    public static bool InProject(JsonElement issue, string requestedKey, string? projectKey)
    {
        if (string.IsNullOrWhiteSpace(projectKey)) return true;
        var key = issue.TryGetProperty("key", out var held) && held.ValueKind == JsonValueKind.String
            ? held.GetString()! : requestedKey;
        var dash = key.LastIndexOf('-');
        return dash > 0 && string.Equals(key[..dash], projectKey, StringComparison.OrdinalIgnoreCase);
    }
}
