using System.Text.Json;
using System.Text.RegularExpressions;
using AgentSmith.Contracts.Reviews;

namespace AgentSmith.Infrastructure.Services.Providers.Pipelines;

/// <summary>
/// 2026-11-17-3435a, moved by 2026-11-18-3863e so a merge request's head pipeline and a deploy's pipeline are told one
/// way: a failed GitLab pipeline's failed jobs, allowed failures left out — stage and name, the failure reason, and the
/// end of each job's trace for at most FailuresRead jobs, linked to the job.
/// <para>2026-11-26-49d3a: the pipeline's test report is read once; a job whose suite (named by the job's group name, so
/// "rspec 1/3" reports as "rspec") has failed or error cases leads with them, and its trace is not read.</para>
/// </summary>
public sealed class GitLabPipelineFailures(PipelineHttp http, string projectApi)
{
    public async Task<IReadOnlyList<CiFailure>> ReadAsync(
        long pipelineId, string? pipelineUrl, CiReadRequest request, CancellationToken ct)
    {
        var jobs = await http.GetAsync($"{projectApi}/pipelines/{pipelineId}/jobs?scope[]=failed&per_page=100", ct);
        var failed = jobs.EnumerateArray().Where(j => !(j.TryGetProperty("allow_failure", out var a) && a.ValueKind == JsonValueKind.True)).ToList();
        var report = failed.Count > 0 && request.FailuresRead > 0 ? await TestReportAsync(pipelineId, ct) : null;
        var failures = new List<CiFailure>();
        var reported = new HashSet<string>(StringComparer.Ordinal); // a suite's cases go with its first failed shard only
        foreach (var job in failed)
        {
            var reason = PipelineHttp.Text(job, "failure_reason") is { } why ? $"failure reason: {why}" : null;
            var tests = failures.Count < request.FailuresRead ? FailedTests(report, PipelineHttp.Text(job, "name"), reported) : [];
            var trace = tests.Count == 0 && failures.Count < request.FailuresRead
                ? await http.TextAsync($"{projectApi}/jobs/{job.GetProperty("id").GetInt64()}/trace", ct) : null;
            failures.Add(new CiFailure($"{PipelineHttp.Text(job, "stage")}: {PipelineHttp.Text(job, "name")}", PipelineHttp.Text(job, "web_url"),
                CiExcerpt.Of([.. tests, reason], trace, request.ExcerptChars)));
        }
        return failures.Count > 0 ? failures : [new CiFailure("pipeline", pipelineUrl, "The pipeline failed; GitLab lists no failed job.")];
    }

    // A pipeline with no test report, or one the host refuses, has none; the trace tail is the evidence then.
    private async Task<JsonElement?> TestReportAsync(long pipelineId, CancellationToken ct)
    {
        try { return await http.GetAsync($"{projectApi}/pipelines/{pipelineId}/test_report", ct); }
        catch (Exception) when (!ct.IsCancellationRequested) { return null; }
    }

    // A suite is named by the job's group name: "rspec 1/3" reports as "rspec".
    private static IReadOnlyList<string> FailedTests(JsonElement? report, string? job, HashSet<string> reported)
    {
        if (report is not { } r || job is null || !r.TryGetProperty("test_suites", out var suites) || suites.ValueKind != JsonValueKind.Array)
            return [];
        var suite = suites.EnumerateArray().FirstOrDefault(s => PipelineHttp.Text(s, "name") is { } name
            && (job == name || Regex.IsMatch(job, $"^{Regex.Escape(name)} \\d+/\\d+$")));
        if (suite.ValueKind != JsonValueKind.Object || !reported.Add(PipelineHttp.Text(suite, "name")!)
            || !suite.TryGetProperty("test_cases", out var cases) || cases.ValueKind != JsonValueKind.Array) return [];
        return CiTestLines.Of([.. cases.EnumerateArray()
            .Where(c => PipelineHttp.Text(c, "status") is "failed" or "error")
            .Select(c => new CiFailedTest(PipelineHttp.Text(c, "status")!,
                string.Join(".", new[] { PipelineHttp.Text(c, "classname"), PipelineHttp.Text(c, "name") }.Where(p => !string.IsNullOrEmpty(p))),
                PipelineHttp.Text(c, "system_output"), PipelineHttp.Text(c, "stack_trace")))]);
    }
}
