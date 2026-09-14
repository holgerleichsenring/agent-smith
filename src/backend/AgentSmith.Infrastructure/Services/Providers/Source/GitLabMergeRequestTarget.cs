using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Providers.Source;

/// <summary>
/// 2026-09-13-a284: reads and changes the <c>target_branch</c> of an already-open GitLab
/// merge request.
/// <para>
/// Its own type rather than a third pair of methods on
/// <see cref="GitLabMergeRequestUpdater"/>: that type is at the line limit the coding
/// principles enforce, and where a merge request POINTS is a different question from what
/// it says and whether it is merged. The iid parser is shared with it, so one URL shape is
/// understood in one place.
/// </para>
/// </summary>
public sealed class GitLabMergeRequestTarget(
    string baseUrl, string projectPath, string privateToken, HttpClient httpClient, ILogger logger)
{
    public async Task<string?> ReadBaseAsync(string prUrl, CancellationToken cancellationToken)
    {
        if (!GitLabMergeRequestUpdater.TryParseMergeRequestIid(prUrl, out var iid)) return null;
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, MergeRequestUrl(iid));
            request.Headers.Add("PRIVATE-TOKEN", privateToken);
            using var response = await httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();
            using var json = JsonDocument.Parse(
                await response.Content.ReadAsStringAsync(cancellationToken));
            return json.RootElement.TryGetProperty("target_branch", out var target)
                ? target.GetString()
                : null;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not read the target branch of MR !{Iid}", iid);
            return null;
        }
    }

    public async Task<bool> MoveAsync(string prUrl, string target, CancellationToken cancellationToken)
    {
        if (!GitLabMergeRequestUpdater.TryParseMergeRequestIid(prUrl, out var iid)) return false;
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Put, MergeRequestUrl(iid))
            {
                Content = JsonContent.Create(new { target_branch = target }),
            };
            request.Headers.Add("PRIVATE-TOKEN", privateToken);
            using var response = await httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();
            logger.LogInformation("MR !{Iid} now targets {Target}", iid, target);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not move MR !{Iid} onto {Target}", iid, target);
            return false;
        }
    }

    private string MergeRequestUrl(int iid) =>
        $"{baseUrl}/api/v4/projects/{projectPath}/merge_requests/{iid}";
}
