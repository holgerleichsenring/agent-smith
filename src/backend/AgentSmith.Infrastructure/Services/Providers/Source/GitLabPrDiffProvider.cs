using System.Net.Http.Json;
using System.Text.Json;
using AgentSmith.Contracts.Providers;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Providers.Source;

/// <summary>
/// Fetches MR diff from GitLab via REST API v4.
/// GET /projects/{id}/merge_requests/{iid}/diffs
/// </summary>
public sealed class GitLabPrDiffProvider(
    HttpClient httpClient,
    string projectId,
    ILogger<GitLabPrDiffProvider> logger) : IPrDiffProvider
{
    public async Task<PrDiff> GetDiffAsync(string prIdentifier, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Fetching MR !{MrIid} diff from GitLab project {ProjectId}", prIdentifier, projectId);

        var mrUrl = $"projects/{Uri.EscapeDataString(projectId)}/merge_requests/{prIdentifier}";
        var mrResponse = await httpClient.GetFromJsonAsync<JsonElement>(mrUrl, cancellationToken);

        var baseSha = mrResponse.GetProperty("diff_refs").GetProperty("base_sha").GetString() ?? string.Empty;
        var headSha = mrResponse.GetProperty("diff_refs").GetProperty("head_sha").GetString() ?? string.Empty;

        var diffsUrl = $"{mrUrl}/diffs";
        var diffs = await httpClient.GetFromJsonAsync<JsonElement[]>(diffsUrl, cancellationToken) ?? [];

        var changedFiles = diffs.Select(d => new ChangedFile(
            d.GetProperty("new_path").GetString() ?? d.GetProperty("old_path").GetString() ?? string.Empty,
            d.GetProperty("diff").GetString() ?? string.Empty,
            d.GetProperty("new_file").GetBoolean() ? ChangeKind.Added
                : d.GetProperty("deleted_file").GetBoolean() ? ChangeKind.Deleted
                : ChangeKind.Modified)).ToList();

        return new PrDiff(baseSha, headSha, changedFiles);
    }
}
