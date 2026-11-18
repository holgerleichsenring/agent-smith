using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Infrastructure.Models;

namespace AgentSmith.Infrastructure.Services.Providers.Pipelines;

/// <summary>
/// 2026-11-18-3863e: GitHub Actions — a workflow_dispatch of the named workflow on a branch or tag, asking GitHub to
/// return the run it started (return_run_details). A host that answers 204 started the run without naming it; that is
/// said with the workflow's runs page, never guessed from the run list. A run is read from actions/runs/{id}.
/// </summary>
public sealed class GitHubPipelineRunner(PipelineHttp http, GitHubRepoAddress address, string? defaultBranch) : IPipelineRunner
{
    private string Repo => $"{address.Api.RestRoot}/repos/{address.Owner}/{address.Repo}";

    public async Task<PipelineRunStart> StartAsync(PipelineRunOrder order, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(order);
        if (string.IsNullOrWhiteSpace(order.Pipeline))
            throw new ArgumentException("GitHub starts a workflow by its file name (deploy.yml) or id; the environment names none");
        var workflow = Uri.EscapeDataString(order.Pipeline.Trim());
        var reference = PipelineRef.Short(order.Ref) ?? defaultBranch ?? await DefaultBranchAsync(cancellationToken);
        using var answer = await http.SendAsync(HttpMethod.Post, $"{Repo}/actions/workflows/{workflow}/dispatches",
            new Dictionary<string, object> { ["ref"] = reference, ["inputs"] = order.Inputs, ["return_run_details"] = true },
            cancellationToken);
        if (answer is null) return new PipelineRunStart(null, $"{address.RepoUrl}/actions/workflows/{workflow}");
        var root = answer.RootElement;
        var id = root.TryGetProperty("workflow_run_id", out var run) ? run.GetRawText().Trim('"') : null;
        return new PipelineRunStart(id, PipelineHttp.Text(root, "html_url") ?? $"{address.RepoUrl}/actions/runs/{id}");
    }

    public async Task<PipelineRunState> ReadAsync(string runId, CiReadRequest bounds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(bounds);
        var run = await http.GetAsync($"{Repo}/actions/runs/{runId}", cancellationToken);
        var url = PipelineHttp.Text(run, "html_url") ?? $"{address.RepoUrl}/actions/runs/{runId}";
        var state = State(PipelineHttp.Text(run, "status"), PipelineHttp.Text(run, "conclusion"));
        return new PipelineRunState(state, url, state == CiStates.Red
            ? await new GitHubRunFailures(http, Repo).ReadAsync(runId, url, PipelineHttp.Text(run, "conclusion"), bounds, cancellationToken)
            : []);
    }

    private static string State(string? status, string? conclusion) =>
        status != "completed" ? CiStates.Pending
        : conclusion is "success" or "skipped" or "neutral" ? CiStates.Green
        : CiStates.Red;

    private async Task<string> DefaultBranchAsync(CancellationToken ct) =>
        PipelineHttp.Text(await http.GetAsync(Repo, ct), "default_branch")
        ?? throw new InvalidOperationException($"GitHub names no default branch of {address.FullName}; give the environment a ref");
}
