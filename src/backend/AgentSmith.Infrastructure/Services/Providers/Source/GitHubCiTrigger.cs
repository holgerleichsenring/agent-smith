using System.Net;
using System.Text;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Infrastructure.Models;
using AgentSmith.Infrastructure.Services.Providers.Pipelines;

namespace AgentSmith.Infrastructure.Services.Providers.Source;

/// <summary>
/// 2026-11-24-f9c5d: GitHub runs a pull request's workflows from their <c>on: pull_request</c> filters, so a filter naming only
/// the default branch leaves a series branch without CI. The active workflows' files on the default branch are read
/// (<see cref="GitHubWorkflowTriggers"/>): none running for the default branch is no CI; one already running for this base is
/// GitHub's own start; otherwise each declaring workflow_dispatch is dispatched on the head branch, so its checks land on the
/// head. Dispatching needs the token's Actions: write.
/// </summary>
public sealed class GitHubCiTrigger(PipelineHttp http, GitHubRepoAddress address) : IPullRequestCiTrigger
{
    private string Repo => $"{address.Api.RestRoot}/repos/{address.Owner}/{address.Repo}";

    public async Task<CiTrigger> StartAsync(string prUrl, string? defaultBranch, CancellationToken cancellationToken)
    {
        if (!GitHubPullRequestUpdater.TryParsePullNumber(prUrl, out var number))
            return CiTrigger.Impossible($"'{prUrl}' is not a GitHub pull request URL.");
        try
        {
            var pr = await http.GetAsync($"{Repo}/pulls/{number}", cancellationToken);
            var head = PipelineHttp.Text(pr.GetProperty("head"), "ref")!;
            var into = PipelineHttp.Text(pr.GetProperty("base"), "ref")!;
            var main = defaultBranch ?? PipelineHttp.Text(await http.GetAsync(Repo, cancellationToken), "default_branch") ?? "main";
            var workflows = await WorkflowsAsync(main, cancellationToken);
            var forMain = workflows.Where(w => w.Triggers.RunsFor(main)).ToList();
            if (forMain.Count == 0) return CiTrigger.None($"no workflow of {address.FullName} runs for pull requests into {main}");
            // A workflow that already takes this base and still reported nothing skipped this head (paths, types, conditions) —
            // as it would for a pull request into the default branch, which then merges as one with no CI.
            if (forMain.Any(w => w.Triggers.RunsFor(into)))
                return CiTrigger.None($"{Names(forMain.Where(w => w.Triggers.RunsFor(into)))} run for pull requests into {into} but reported nothing on this head");
            var dispatchable = forMain.Where(w => w.Triggers.Dispatch).ToList();
            if (dispatchable.Count == 0)
                return CiTrigger.Impossible($"{Names(forMain)} run for pull requests into {main} only and declare no workflow_dispatch; add "
                    + "'agent-smith/series/**' to their pull_request branches, or a workflow_dispatch trigger, so a series branch gets CI");
            foreach (var w in dispatchable)
                await http.SendAsync(HttpMethod.Post, $"{Repo}/actions/workflows/{w.Id}/dispatches", new Dictionary<string, object> { ["ref"] = head }, cancellationToken);
            return CiTrigger.Started($"dispatched {Names(dispatchable)} on {head}; a job that runs only for pull_request events is skipped there",
                $"{address.RepoUrl}/actions");
        }
        catch (HttpRequestException ex) when (ex.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound or HttpStatusCode.Unauthorized)
        {
            return CiTrigger.Impossible($"GitHub refused to read or start the workflows of {address.FullName} ({(int)ex.StatusCode!}): "
                + "the token needs Actions: write (classic token: repo)");
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            return CiTrigger.Impossible($"GitHub did not start the workflows: {ex.Message.Split('\n', 2)[0].Trim()}");
        }
    }

    private async Task<IReadOnlyList<(long Id, string Path, GitHubWorkflowTriggers Triggers)>> WorkflowsAsync(string main, CancellationToken ct)
    {
        var listed = await http.GetAsync($"{Repo}/actions/workflows?per_page=100", ct);
        var found = new List<(long, string, GitHubWorkflowTriggers)>();
        foreach (var w in listed.GetProperty("workflows").EnumerateArray().Where(w => PipelineHttp.Text(w, "state") == "active"))
        {
            var path = PipelineHttp.Text(w, "path") ?? string.Empty;
            if (!path.StartsWith(".github/workflows/", StringComparison.Ordinal)) continue; // dynamic workflows (Dependabot, Pages) have no file
            if (await FileAsync(path, main, ct) is { } yaml) found.Add((w.GetProperty("id").GetInt64(), path, GitHubWorkflowTriggers.Parse(yaml)));
        }
        return found;
    }

    // A workflow file the default branch does not hold (it lives on another branch) runs no pull request into it.
    private async Task<string?> FileAsync(string path, string main, CancellationToken ct)
    {
        try
        {
            var file = await http.GetAsync($"{Repo}/contents/{path}?ref={Uri.EscapeDataString(main)}", ct);
            return Encoding.UTF8.GetString(Convert.FromBase64String((PipelineHttp.Text(file, "content") ?? string.Empty).Replace("\n", string.Empty)));
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound) { return null; }
    }

    private static string Names(IEnumerable<(long Id, string Path, GitHubWorkflowTriggers Triggers)> workflows) =>
        string.Join(", ", workflows.Select(w => w.Path[(w.Path.LastIndexOf('/') + 1)..]));
}
