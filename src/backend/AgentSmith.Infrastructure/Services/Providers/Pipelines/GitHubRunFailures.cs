using System.Text.Json;
using AgentSmith.Contracts.Reviews;

namespace AgentSmith.Infrastructure.Services.Providers.Pipelines;

/// <summary>
/// 2026-11-18-3863e: why a GitHub Actions run failed — its latest attempt's failed jobs, each with its failed steps and,
/// for at most the bound's FailuresRead jobs, the end of its log, linked to the job.
/// </summary>
public sealed class GitHubRunFailures(PipelineHttp http, string repoApi)
{
    private static readonly string[] Failed = ["failure", "timed_out", "cancelled", "startup_failure"];

    public async Task<IReadOnlyList<CiFailure>> ReadAsync(
        string runId, string runUrl, string? conclusion, CiReadRequest bounds, CancellationToken ct)
    {
        var jobs = await http.GetAsync($"{repoApi}/actions/runs/{runId}/jobs?filter=latest&per_page=100", ct);
        var failures = new List<CiFailure>();
        foreach (var job in Jobs(jobs).Where(j => Failed.Contains(PipelineHttp.Text(j, "conclusion") ?? "")))
        {
            var log = failures.Count < bounds.FailuresRead
                ? await http.TextAsync($"{repoApi}/actions/jobs/{job.GetProperty("id").GetRawText()}/logs", ct) : null;
            failures.Add(new CiFailure(PipelineHttp.Text(job, "name") ?? "job", PipelineHttp.Text(job, "html_url"),
                CiExcerpt.Of(Steps(job), log, bounds.ExcerptChars)));
        }
        return failures.Count > 0 ? failures
            : [new CiFailure("workflow run", runUrl, $"The run ended {conclusion}; GitHub lists no failed job.")];
    }

    private static IEnumerable<JsonElement> Jobs(JsonElement page) =>
        page.TryGetProperty("jobs", out var jobs) && jobs.ValueKind == JsonValueKind.Array ? jobs.EnumerateArray() : [];

    private static IEnumerable<string?> Steps(JsonElement job) =>
        job.TryGetProperty("steps", out var steps) && steps.ValueKind == JsonValueKind.Array
            ? steps.EnumerateArray().Where(s => Failed.Contains(PipelineHttp.Text(s, "conclusion") ?? ""))
                .Select(s => $"step failed: {PipelineHttp.Text(s, "name")}")
            : [];
}
