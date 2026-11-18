using System.Text.Json;
using AgentSmith.Contracts.Reviews;

namespace AgentSmith.Infrastructure.Services.Providers.Pipelines;

/// <summary>
/// 2026-11-17-3435a, moved by 2026-11-18-3863e so a merge request's head pipeline and a deploy's pipeline are told one
/// way: a failed GitLab pipeline's failed jobs, allowed failures left out — stage and name, the failure reason, and the
/// end of each job's trace for at most FailuresRead jobs, linked to the job.
/// </summary>
public sealed class GitLabPipelineFailures(PipelineHttp http, string projectApi)
{
    public async Task<IReadOnlyList<CiFailure>> ReadAsync(
        long pipelineId, string? pipelineUrl, CiReadRequest request, CancellationToken ct)
    {
        var jobs = await http.GetAsync($"{projectApi}/pipelines/{pipelineId}/jobs?scope[]=failed&per_page=100", ct);
        var failures = new List<CiFailure>();
        foreach (var job in jobs.EnumerateArray().Where(j => !(j.TryGetProperty("allow_failure", out var a) && a.ValueKind == JsonValueKind.True)))
        {
            var trace = failures.Count < request.FailuresRead
                ? await http.TextAsync($"{projectApi}/jobs/{job.GetProperty("id").GetInt64()}/trace", ct) : null;
            failures.Add(new CiFailure($"{PipelineHttp.Text(job, "stage")}: {PipelineHttp.Text(job, "name")}", PipelineHttp.Text(job, "web_url"),
                CiExcerpt.Of([PipelineHttp.Text(job, "failure_reason") is { } why ? $"failure reason: {why}" : null], trace, request.ExcerptChars)));
        }
        return failures.Count > 0 ? failures : [new CiFailure("pipeline", pipelineUrl, "The pipeline failed; GitLab lists no failed job.")];
    }
}
