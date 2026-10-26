using System.Text.Json;
using AgentSmith.Infrastructure.Models;

namespace AgentSmith.Server.Services.Webhooks;

/// <summary>2026-10-08-e8b9c: the repository and pull-request URLs an Azure DevOps PR payload names,
/// in the shape the Azure Repos provider builds and parses, and the reviewers at a given vote.</summary>
public static class AzureDevOpsPrUrl
{
    /// <summary>2026-10-26-cb25a: the repository's web URL as <see cref="AzureDevOpsRepoAddress"/> writes it (no user
    /// info, port kept); a remote that names no Azure Repos repository is returned as the payload wrote it.</summary>
    public static string RepositoryUrl(JsonElement pr)
    {
        var remote = PayloadActTime.Text(pr, "repository", "remoteUrl") ?? string.Empty;
        return AzureDevOpsRepoAddress.TryParse(remote, out var address) ? address!.RepoUrl : remote.TrimEnd('/');
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
