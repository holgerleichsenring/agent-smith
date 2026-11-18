using System.Globalization;
using System.Text.Json;
using AgentSmith.Contracts.Reviews;

namespace AgentSmith.Infrastructure.Services.Providers.Source;

/// <summary>
/// 2026-11-17-3435a: a GitLab merge request's CI — its head pipeline, decided per pipeline as GitLab does (a running
/// pipeline with a failed job is pending until GitLab calls it failed). A red one is read with its failed jobs, allowed
/// failures left out: stage and name, the failure reason, and the end of each job's trace for at most FailuresRead jobs.
/// </summary>
public sealed class GitLabCiReader(string baseUrl, string projectPath, string token, HttpClient http)
{
    private string Api => $"{baseUrl}/api/v4/projects/{projectPath}";

    public async Task<PullRequestCi> ReadAsync(string prUrl, CiReadRequest request, CancellationToken ct)
    {
        if (!GitLabMergeRequestUpdater.TryParseMergeRequestIid(prUrl, out var iid))
            throw new ArgumentException($"Not a GitLab merge request URL: {prUrl}", nameof(prUrl));
        using var mr = await GetJsonAsync($"{Api}/merge_requests/{iid}", ct);
        var head = request.HeadSha ?? Str(mr.RootElement, "sha");
        if (!mr.RootElement.TryGetProperty("head_pipeline", out var pipeline) || pipeline.ValueKind != JsonValueKind.Object)
            return PullRequestCi.NoneOn(head);
        var at = Time(pipeline, "finished_at") ?? Time(pipeline, "updated_at");
        var outcome = Str(pipeline, "sha") != head ? CiCheckOutcome.Pending : Outcome(Str(pipeline, "status"));
        var state = CiVerdict.Of([new CiCheck("pipeline", outcome, false, at)]);
        return new PullRequestCi(state, head, at, state == CiStates.Red
            ? await FailuresAsync(pipeline.GetProperty("id").GetInt64(), Str(pipeline, "web_url"), request, ct) : []);
    }

    private static CiCheckOutcome Outcome(string? status) => status switch
    {
        "success" or "skipped" => CiCheckOutcome.Passed,
        "failed" or "canceled" => CiCheckOutcome.Failed,
        _ => CiCheckOutcome.Pending,
    };

    private async Task<IReadOnlyList<CiFailure>> FailuresAsync(long pipelineId, string? pipelineUrl, CiReadRequest request, CancellationToken ct)
    {
        using var jobs = await GetJsonAsync($"{Api}/pipelines/{pipelineId}/jobs?scope[]=failed&per_page=100", ct);
        var failures = new List<CiFailure>();
        foreach (var job in jobs.RootElement.EnumerateArray().Where(j => !(j.TryGetProperty("allow_failure", out var a) && a.ValueKind == JsonValueKind.True)))
        {
            var trace = failures.Count < request.FailuresRead ? await TraceAsync(job.GetProperty("id").GetInt64(), ct) : null;
            failures.Add(new CiFailure($"{Str(job, "stage")}: {Str(job, "name")}", Str(job, "web_url"),
                CiExcerpt.Of([Str(job, "failure_reason") is { } why ? $"failure reason: {why}" : null], trace, request.ExcerptChars)));
        }
        return failures.Count > 0 ? failures : [new CiFailure("pipeline", pipelineUrl, "The pipeline failed; GitLab lists no failed job.")];
    }

    // A trace that cannot be read costs the tail, not the evidence.
    private async Task<string?> TraceAsync(long jobId, CancellationToken ct)
    {
        using var request = Get($"{Api}/jobs/{jobId}/trace");
        using var response = await http.SendAsync(request, ct);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadAsStringAsync(ct)
            : $"(trace not readable: GitLab answered {(int)response.StatusCode})";
    }

    private async Task<JsonDocument> GetJsonAsync(string url, CancellationToken ct)
    {
        using var request = Get(url);
        using var response = await http.SendAsync(request, ct);
        await response.EnsureSuccessWithBodyAsync(ct);
        return await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
    }

    private HttpRequestMessage Get(string url)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("PRIVATE-TOKEN", token);
        return request;
    }

    private static DateTimeOffset? Time(JsonElement e, string name) =>
        Str(e, name) is { } at ? DateTimeOffset.Parse(at, CultureInfo.InvariantCulture) : null;

    private static string? Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
}
