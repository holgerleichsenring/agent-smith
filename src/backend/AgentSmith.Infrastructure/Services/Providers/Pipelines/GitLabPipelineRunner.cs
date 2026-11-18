using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Reviews;

namespace AgentSmith.Infrastructure.Services.Providers.Pipelines;

/// <summary>
/// 2026-11-18-3863e: GitLab — a new pipeline of the project on a branch or tag with the inputs as pipeline variables
/// (POST /projects/:id/pipeline). GitLab runs the project's own .gitlab-ci.yml, so a named pipeline is refused rather
/// than ignored. A project restricting pipeline variables answers 400/403, said in GitLab's words.
/// </summary>
public sealed class GitLabPipelineRunner(PipelineHttp http, string projectApi, string? defaultBranch) : IPipelineRunner
{
    public async Task<PipelineRunStart> StartAsync(PipelineRunOrder order, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(order);
        if (!string.IsNullOrWhiteSpace(order.Pipeline))
            throw new ArgumentException($"GitLab runs the project's own pipeline for a ref; the environment names pipeline "
                + $"'{order.Pipeline}', which GitLab cannot pick. Remove it from the environment");
        var reference = PipelineRef.Short(order.Ref) ?? defaultBranch ?? await DefaultBranchAsync(cancellationToken);
        var variables = order.Inputs.Select(i => new Dictionary<string, string> { ["key"] = i.Key, ["value"] = i.Value }).ToList();
        using var answer = await http.SendAsync(HttpMethod.Post, $"{projectApi}/pipeline",
            new Dictionary<string, object> { ["ref"] = reference, ["variables"] = variables }, cancellationToken)
            ?? throw new InvalidOperationException("GitLab started no pipeline: it answered no content");
        var root = answer.RootElement;
        var id = root.GetProperty("id").GetInt64().ToString(System.Globalization.CultureInfo.InvariantCulture);
        return new PipelineRunStart(id, PipelineHttp.Text(root, "web_url") ?? $"{projectApi}/pipelines/{id}");
    }

    public async Task<PipelineRunState> ReadAsync(string runId, CiReadRequest bounds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(bounds);
        var pipeline = await http.GetAsync($"{projectApi}/pipelines/{runId}", cancellationToken);
        var url = PipelineHttp.Text(pipeline, "web_url") ?? $"{projectApi}/pipelines/{runId}";
        var state = CiVerdict.Of([new CiCheck("pipeline", GitLabPipelineStatus.Outcome(PipelineHttp.Text(pipeline, "status")), false, null)]);
        return new PipelineRunState(state, url, state == CiStates.Red
            ? await new GitLabPipelineFailures(http, projectApi).ReadAsync(pipeline.GetProperty("id").GetInt64(), url, bounds, cancellationToken)
            : []);
    }

    private async Task<string> DefaultBranchAsync(CancellationToken ct) =>
        PipelineHttp.Text(await http.GetAsync(projectApi, ct), "default_branch")
        ?? throw new InvalidOperationException("GitLab names no default branch of the project; give the environment a ref");
}
