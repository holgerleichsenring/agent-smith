using System.Net.Http.Json;
using AgentSmith.Contracts.Providers;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Providers.Source;

/// <summary>
/// p0490: changes the state of an already-opened GitLab merge request, addressed by
/// the web URL <c>CreatePullRequestAsync</c> handed back — its description, its draft
/// title prefix, or whether it is merged. Opening a merge request needs a project, a
/// branch and a default-branch lookup; changing one that already exists needs only the
/// iid recovered from that URL, which is why the three edits share this type and one
/// parser instead of three copies inside the provider.
/// </summary>
public sealed class GitLabMergeRequestUpdater(
    string baseUrl, string projectPath, string privateToken, HttpClient httpClient, ILogger logger)
{
    public async Task<bool> UpdateBodyAsync(
        string prUrl, string newBody, CancellationToken cancellationToken)
    {
        if (!TryParseMergeRequestIid(prUrl, out var iid)) return false;
        try
        {
            using var response = await PutAsync(
                MergeRequestUrl(iid), new { description = newBody }, cancellationToken);
            response.EnsureSuccessStatusCode();
            logger.LogInformation("Updated MR body for !{Iid}", iid);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to update MR body for !{Iid}", iid);
            return false;
        }
    }

    // p0393a: GitLab has no draft FLAG — a merge request is a draft while its title
    // starts with "Draft:". Taking it out of draft is therefore a title edit.
    public async Task<bool> MarkReadyAsync(string prUrl, CancellationToken cancellationToken)
    {
        if (!TryParseMergeRequestIid(prUrl, out var iid)) return false;
        try
        {
            using var response = await PutAsync(
                MergeRequestUrl(iid),
                new { remove_source_branch = false, draft = false }, cancellationToken);
            response.EnsureSuccessStatusCode();
            logger.LogInformation("MR !{Iid} is out of draft — the sequence completed", iid);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to mark MR !{Iid} ready for review", iid);
            return false;
        }
    }

    // p0490: GitLab answers a merge it will not perform with 405 ("Method Not Allowed"
    // — approvals missing, pipeline not green, branch protected) or 406 (conflict), and
    // puts the sentence in the response body. That body IS the reason the operator
    // needs, so it is read out and reported as a refusal, not thrown at a finished run.
    public async Task<PullRequestCompletion> CompleteAsync(
        string prUrl, CancellationToken cancellationToken)
    {
        if (!TryParseMergeRequestIid(prUrl, out var iid))
            return PullRequestCompletion.Refused($"'{prUrl}' is not a GitLab merge request URL.");
        try
        {
            using var response = await PutAsync(
                $"{MergeRequestUrl(iid)}/merge",
                new { should_remove_source_branch = false }, cancellationToken);
            var answer = GitLabMergeAnswer.Parse(
                await response.Content.ReadAsStringAsync(cancellationToken));
            if (!response.IsSuccessStatusCode)
                return Refuse(iid, $"GitLab answered {(int)response.StatusCode}: {answer.Message}");
            if (answer.State is { } state && state != "merged")
                return Refuse(iid, $"GitLab left the merge request in state '{state}'.");
            logger.LogInformation("MR !{Iid} merged", iid);
            return PullRequestCompletion.Merged();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Merging MR !{Iid} was refused", iid);
            return PullRequestCompletion.Refused(ex.Message);
        }
    }

    private PullRequestCompletion Refuse(int iid, string reason)
    {
        logger.LogInformation("MR !{Iid} was not merged: {Reason}", iid, reason);
        return PullRequestCompletion.Refused(reason);
    }

    private Task<HttpResponseMessage> PutAsync(string url, object payload, CancellationToken ct)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, url) { Content = JsonContent.Create(payload) };
        request.Headers.Add("PRIVATE-TOKEN", privateToken);
        return httpClient.SendAsync(request, ct);
    }

    private string MergeRequestUrl(int iid) =>
        $"{baseUrl}/api/v4/projects/{projectPath}/merge_requests/{iid}";

    private static bool TryParseMergeRequestIid(string mrUrl, out int iid)
    {
        iid = 0;
        var match = System.Text.RegularExpressions.Regex.Match(mrUrl, @"/merge_requests/(\d+)");
        return match.Success && int.TryParse(match.Groups[1].Value, out iid);
    }
}
