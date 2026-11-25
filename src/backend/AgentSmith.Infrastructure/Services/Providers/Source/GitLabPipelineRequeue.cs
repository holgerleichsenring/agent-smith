using System.Text.Json;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Infrastructure.Services.Providers.Pipelines;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Providers.Source;

/// <summary>
/// 2026-11-25-7a92b: queues a GitLab merge request's CI again where its head has not started (<see cref="GitLabHeadPipeline"/>,
/// read with the caller's settled flag): a head without a pipeline gets one through the start f9c5d built for series branches
/// (<see cref="GitLabCiTrigger"/>: a merge-request pipeline, refused a branch pipeline). A pipeline blocked on a manual job is left
/// — a new pipeline would block there again; a person runs the job. A merge request from another project is never queued.
/// </summary>
public sealed class GitLabPipelineRequeue(PipelineHttp http, string projectApi, ILogger logger)
{
    public async Task<int> RequeueAsync(string prUrl, bool headSettled, CancellationToken ct)
    {
        if (!GitLabMergeRequestUpdater.TryParseMergeRequestIid(prUrl, out var iid))
            throw new ArgumentException($"Not a GitLab merge request URL: {prUrl}", nameof(prUrl));
        var mr = await http.GetAsync($"{projectApi}/merge_requests/{iid}", ct);
        var head = PipelineHttp.Text(mr, "sha");
        if (GitLabHeadPipeline.Outcome(mr, head, headSettled) != CiCheckOutcome.NotStarted) return 0;
        if (Id(mr, "source_project_id") != Id(mr, "target_project_id"))
        {
            logger.LogInformation("MR !{Iid} comes from another project; its CI is not started by agent-smith", iid);
            return 0;
        }
        if (GitLabHeadPipeline.HasPipelineOn(mr, head))
        {
            logger.LogInformation("MR !{Iid}: the pipeline on {Head} waits for a manual job; a person runs it", iid, head);
            return 0;
        }
        var started = await new GitLabCiTrigger(http, projectApi).StartAsync(prUrl, null, ct);
        logger.LogInformation("MR !{Iid}: no pipeline had started on {Head}: {Outcome} — {Text}", iid, head, started.Outcome, started.Text);
        return started.Outcome == CiTriggerOutcome.Started ? 1 : 0;
    }

    private static long? Id(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt64() : null;
}
