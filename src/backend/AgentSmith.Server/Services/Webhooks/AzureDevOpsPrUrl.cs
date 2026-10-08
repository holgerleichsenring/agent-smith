using System.Text.Json;

namespace AgentSmith.Server.Services.Webhooks;

/// <summary>2026-10-08-e8b9c: the repository and pull-request URLs an Azure DevOps PR payload names,
/// in the shape the Azure Repos provider builds and parses, and the reviewers at a given vote.</summary>
public static class AzureDevOpsPrUrl
{
    public static string RepositoryUrl(JsonElement pr)
    {
        var remote = PayloadActTime.Text(pr, "repository", "remoteUrl") ?? string.Empty;
        return Uri.TryCreate(remote, UriKind.Absolute, out var uri)
            ? $"{uri.Scheme}://{uri.Host}{uri.AbsolutePath}".TrimEnd('/')
            : remote;
    }

    public static string Of(string repositoryUrl, JsonElement pr) =>
        $"{repositoryUrl}/pullrequest/{pr.GetProperty("pullRequestId").GetInt32()}";

    public static HashSet<string> VotersAt(JsonElement pr, int vote) =>
        pr.TryGetProperty("reviewers", out var reviewers) && reviewers.ValueKind == JsonValueKind.Array
            ? [.. reviewers.EnumerateArray()
                .Where(r => r.TryGetProperty("vote", out var v) && v.ValueKind == JsonValueKind.Number && v.GetInt32() == vote)
                .Select(r => PayloadActTime.Text(r, "id")).OfType<string>()]
            : [];
}
