using System.Net.Http.Json;
using System.Text.Json;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Providers.Source;

/// <summary>
/// 2026-11-17-023fa: a code run's merge of a GitLab merge request. Bypass merges now through the same PUT init uses;
/// GitLab has no API bypass for required approvals or pipelines, so a refusal names what to grant or the key that
/// waits instead. Respect asks GitLab to merge when the pipeline succeeds (auto_merge on newer servers) and reads the
/// answer: merged now, armed, or refused.
/// </summary>
public sealed class GitLabMergeModes(
    string baseUrl, string projectPath, string privateToken, HttpClient httpClient,
    GitLabMergeRequestUpdater updater, ILogger logger)
{
    public const string Policy = "the merge checks (approvals, pipeline, protected branch)";
    public const string Permission = "the token a role the merge checks let merge (or relax the project's merge checks)";

    public async Task<PullRequestCompletion> MergeAsync(string prUrl, BranchProtection protection, CancellationToken ct)
    {
        if (protection == BranchProtection.Respect) return await ArmAsync(prUrl, ct);
        var completion = await updater.CompleteAsync(prUrl, ct);
        return completion.Outcome == PullRequestCompletionOutcome.Refused
            ? BypassRefusal.Of(Policy, "GitLab", completion.Reason ?? "GitLab refused the merge", Permission)
            : completion;
    }

    private async Task<PullRequestCompletion> ArmAsync(string prUrl, CancellationToken ct)
    {
        if (!GitLabMergeRequestUpdater.TryParseMergeRequestIid(prUrl, out var iid))
            return PullRequestCompletion.Refused($"'{prUrl}' is not a GitLab merge request URL.");
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Put,
                $"{baseUrl}/api/v4/projects/{projectPath}/merge_requests/{iid}/merge")
            {
                Content = JsonContent.Create(new
                {
                    should_remove_source_branch = true, merge_when_pipeline_succeeds = true, auto_merge = true,
                }),
            };
            request.Headers.Add("PRIVATE-TOKEN", privateToken);
            using var response = await httpClient.SendAsync(request, ct);
            var body = await response.Content.ReadAsStringAsync(ct);
            return Interpret(iid, (int)response.StatusCode, response.IsSuccessStatusCode, body);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Arming MR !{Iid} was refused", iid);
            return PullRequestCompletion.Refused(ex.Message);
        }
    }

    /// <summary>GitLab's answer to a merge-when-pipeline-succeeds request.</summary>
    public static PullRequestCompletion Interpret(int iid, int status, bool success, string body)
    {
        var answer = GitLabMergeAnswer.Parse(body);
        if (!success) return PullRequestCompletion.Refused($"GitLab answered {status}: {answer.Message}");
        if (answer.State == "merged") return PullRequestCompletion.Merged();
        return Armed(body)
            ? PullRequestCompletion.Armed($"GitLab will merge !{iid} when its pipeline succeeds.")
            : PullRequestCompletion.Refused($"GitLab left !{iid} in state '{answer.State}' without arming the merge.");
    }

    private static bool Armed(string body)
    {
        try
        {
            using var json = JsonDocument.Parse(body);
            var root = json.RootElement;
            return True(root, "merge_when_pipeline_succeeds") || True(root, "auto_merge_enabled") || True(root, "auto_merge");
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool True(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;
}
