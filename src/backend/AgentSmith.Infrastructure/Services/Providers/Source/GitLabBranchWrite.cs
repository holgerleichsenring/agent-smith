using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AgentSmith.Contracts.Providers;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Providers.Source;

/// <summary>
/// 2026-09-22-b6ad: one commit carrying every file, through the SAME HttpClient and private token
/// the provider already reads a file with. GitLab's commits endpoint takes a list of ACTIONS and a
/// branch in one request, so the whole set lands as one commit; <c>start_branch</c> is what creates
/// the branch, and it is sent only when the branch does not exist yet — sending it for an existing
/// branch asks GitLab to re-cut it.
/// <para>
/// Each action is <c>create</c> or <c>update</c> depending on whether that path is already on the
/// branch: GitLab refuses a <c>create</c> for a file that exists and an <c>update</c> for one that
/// does not, so the two are decided per file rather than per commit. 2026-11-23-c81ea: a deletion is a
/// <c>delete</c> action, and no action carries <c>last_commit_id</c> — it is a per-file check, so the head
/// would refuse ordinary updates. A commit that lost the race on the ref (400 'Could not update
/// refs/heads/…') is a <see cref="BranchWriteResult.Conflict"/>.
/// </para>
/// </summary>
public sealed class GitLabBranchWrite(
    string baseUrl, string projectPath, string privateToken, HttpClient httpClient, ILogger logger)
{
    private const string RefRaceMarker = "Could not update refs/heads/";
    private readonly GitLabFileProbe _probe = new(baseUrl, projectPath, privateToken, httpClient);
    private readonly AbsentDeletionFilter _deletions = new(logger);

    public async Task<BranchWriteResult> WriteAsync(
        string branch, string defaultBranch, IReadOnlyList<RepoFile> files, string message,
        CancellationToken cancellationToken)
    {
        if (files.Count == 0) return BranchWriteResult.Failed("no files to write");
        try
        {
            var exists = await _probe.BranchExistsAsync(branch, cancellationToken);
            var at = exists ? branch : defaultBranch;
            var filtered = await _deletions.FilterAsync(
                files, (path, ct) => _probe.FileExistsStrictAsync(at, path, ct), cancellationToken);
            if (filtered.Refusal is { } refusal) return refusal;
            var actions = new List<object>(filtered.Files.Count);
            foreach (var file in filtered.Files)
                actions.Add(await ActionAsync(file, exists, branch, cancellationToken));
            return filtered.WithNotes(
                await PostAsync(branch, exists ? null : defaultBranch, message, actions, cancellationToken));
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "{Project}: writing {Branch} failed", projectPath, branch);
            return BranchWriteResult.Failed(ex.Message.Split('\n', 2)[0].Trim());
        }
    }

    private async Task<object> ActionAsync(RepoFile file, bool branchExists, string branch, CancellationToken ct)
    {
        if (file.Delete) return new { action = "delete", file_path = file.Path };
        var update = branchExists && await _probe.FileExistsLenientAsync(branch, file.Path, ct);
        return new { action = update ? "update" : "create", file_path = file.Path, content = file.Content };
    }

    private async Task<BranchWriteResult> PostAsync(
        string branch, string? startBranch, string message, IReadOnlyList<object> actions,
        CancellationToken ct)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post, $"{baseUrl}/api/v4/projects/{projectPath}/repository/commits");
        request.Headers.Add("PRIVATE-TOKEN", privateToken);
        var payload = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["branch"] = branch,
            ["commit_message"] = message,
            ["actions"] = actions,
        };
        // Only when the branch is absent: start_branch on an existing branch asks GitLab to re-cut it.
        if (startBranch is not null) payload["start_branch"] = startBranch;
        request.Content = JsonContent.Create(payload);
        using var response = await httpClient.SendAsync(request, ct);
        return Answer(response.StatusCode, await response.Content.ReadAsStringAsync(ct), branch, actions.Count);
    }

    private BranchWriteResult Answer(HttpStatusCode status, string body, string branch, int count)
    {
        if (status == HttpStatusCode.BadRequest && body.Contains(RefRaceMarker, StringComparison.Ordinal))
            return BranchWriteResult.Conflicted($"`{branch}` moved while the commit was made: {Trim(body)}");
        if ((int)status is < 200 or > 299)
            return BranchWriteResult.Failed($"GitLab refused the commit ({(int)status}): {Trim(body)}");
        using var json = JsonDocument.Parse(body);
        if (!json.RootElement.TryGetProperty("id", out var id) || id.GetString() is not { Length: > 0 } sha)
            return BranchWriteResult.Failed("GitLab accepted the commit but named no sha");
        logger.LogInformation("{Project}: {Count} file(s) committed as {Sha} on {Branch}", projectPath, count, sha, branch);
        return BranchWriteResult.Ok(sha);
    }

    private static string Trim(string body) =>
        body.Length <= 300 ? body.Trim() : body[..300].Trim();
}
