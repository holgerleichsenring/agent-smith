using System.Net.Http.Json;
using System.Text.Json;
using AgentSmith.Contracts.Providers;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Providers.Source;

/// <summary>
/// Fetches PR diff from Azure DevOps via REST API.
/// GET /{org}/{project}/_apis/git/repositories/{repo}/pullRequests/{id}/iterations/{iter}/changes
/// </summary>
public sealed class AzureDevOpsPrDiffProvider(
    HttpClient httpClient,
    string organization,
    string project,
    string repositoryId,
    ILogger<AzureDevOpsPrDiffProvider> logger) : IPrDiffProvider
{
    public async Task<PrDiff> GetDiffAsync(string prIdentifier, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Fetching PR #{PrId} diff from Azure DevOps {Org}/{Project}",
            prIdentifier, organization, project);

        var prUrl = $"{organization}/{project}/_apis/git/repositories/{repositoryId}" +
                    $"/pullRequests/{prIdentifier}?api-version=7.1";
        var pr = await httpClient.GetFromJsonAsync<JsonElement>(prUrl, cancellationToken);

        var baseSha = pr.GetProperty("lastMergeTargetCommit").GetProperty("commitId").GetString() ?? string.Empty;
        var headSha = pr.GetProperty("lastMergeSourceCommit").GetProperty("commitId").GetString() ?? string.Empty;

        var iterationsUrl = $"{organization}/{project}/_apis/git/repositories/{repositoryId}" +
                            $"/pullRequests/{prIdentifier}/iterations?api-version=7.1";
        var iterations = await httpClient.GetFromJsonAsync<JsonElement>(iterationsUrl, cancellationToken);
        var iterationCount = iterations.GetProperty("count").GetInt32();

        var changesUrl = $"{organization}/{project}/_apis/git/repositories/{repositoryId}" +
                         $"/pullRequests/{prIdentifier}/iterations/{iterationCount}/changes?api-version=7.1";
        var changes = await httpClient.GetFromJsonAsync<JsonElement>(changesUrl, cancellationToken);

        var changedFiles = changes.GetProperty("changeEntries").EnumerateArray()
            .Select(c => new ChangedFile(
                c.GetProperty("item").GetProperty("path").GetString()?.TrimStart('/') ?? string.Empty,
                string.Empty,
                MapChangeType(c.GetProperty("changeType").GetString())))
            .ToList();

        return new PrDiff(baseSha, headSha, changedFiles);
    }

    private static ChangeKind MapChangeType(string? changeType) => changeType switch
    {
        "add" => ChangeKind.Added,
        "delete" => ChangeKind.Deleted,
        _ => ChangeKind.Modified
    };
}
