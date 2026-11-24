using System.Net;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Infrastructure.Services.Providers.Pipelines;

namespace AgentSmith.Infrastructure.Services.Providers.Source;

/// <summary>
/// 2026-11-24-f9c5d: GitLab runs the project's CI file; a merge request into a series branch may get no pipeline when its
/// rules name the default branch. A project whose CI file is missing on the default branch has no CI (a file in another
/// project, <c>path@group/project</c>, counts as present). Otherwise a merge-request pipeline is created; refused, a branch
/// pipeline on the source branch — which the merge request reads as its head pipeline; refused too, GitLab's words say why.
/// Needs the token's api scope.
/// </summary>
public sealed class GitLabCiTrigger(PipelineHttp http, string projectApi) : IPullRequestCiTrigger
{
    public async Task<CiTrigger> StartAsync(string prUrl, string? defaultBranch, CancellationToken cancellationToken)
    {
        if (!GitLabMergeRequestUpdater.TryParseMergeRequestIid(prUrl, out var iid))
            return CiTrigger.Impossible($"'{prUrl}' is not a GitLab merge request URL.");
        try
        {
            var project = await http.GetAsync(projectApi, cancellationToken);
            var main = defaultBranch ?? PipelineHttp.Text(project, "default_branch") ?? "main";
            var file = PipelineHttp.Text(project, "ci_config_path") is { Length: > 0 } custom ? custom : ".gitlab-ci.yml";
            if (!await CiFileAsync(file, main, cancellationToken)) return CiTrigger.None($"the project has no CI file ({file}) on {main}");
            return await MergeRequestPipelineAsync(iid, cancellationToken) ?? await BranchPipelineAsync(iid, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            return CiTrigger.Impossible($"GitLab started no pipeline: {ex.Message.Split('\n', 2)[0].Trim()} (the token needs the api scope)");
        }
    }

    private async Task<bool> CiFileAsync(string file, string main, CancellationToken ct)
    {
        if (file.Contains('@') || file.Contains("://", StringComparison.Ordinal)) return true;
        try
        {
            await http.GetAsync($"{projectApi}/repository/files/{Uri.EscapeDataString(file)}?ref={Uri.EscapeDataString(main)}", ct);
            return true;
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound) { return false; }
    }

    private async Task<CiTrigger?> MergeRequestPipelineAsync(int iid, CancellationToken ct)
    {
        try
        {
            using var created = await http.SendAsync(HttpMethod.Post, $"{projectApi}/merge_requests/{iid}/pipelines", null, ct);
            return CiTrigger.Started("created a merge-request pipeline", created is null ? null : PipelineHttp.Text(created.RootElement, "web_url"));
        }
        catch (HttpRequestException ex) when (ex.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity or HttpStatusCode.Forbidden)
        {
            return null;
        }
    }

    private async Task<CiTrigger> BranchPipelineAsync(int iid, CancellationToken ct)
    {
        var source = PipelineHttp.Text(await http.GetAsync($"{projectApi}/merge_requests/{iid}", ct), "source_branch")!;
        using var created = await http.SendAsync(HttpMethod.Post, $"{projectApi}/pipeline", new Dictionary<string, object> { ["ref"] = source }, ct);
        return CiTrigger.Started($"created a pipeline on {source} (the project runs no merge-request pipeline for this target)",
            created is null ? null : PipelineHttp.Text(created.RootElement, "web_url"));
    }
}
