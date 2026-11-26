using AgentSmith.Contracts.Reviews;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.TeamFoundation.TestManagement.WebApi;

namespace AgentSmith.Infrastructure.Services.Providers.Source;

/// <summary>
/// 2026-11-17-3435a: why a build failed — its failed task records, each with its error issues and the end of its log,
/// linked to the build's results page. Read only for at most the request's FailuresRead builds.
/// <para>2026-11-26-49d3a: the build's failed test results lead when there are any (<see cref="AzureReposTestEvidence"/>), and
/// then no log is read; otherwise the log tail is the FIRST failed task's — the last is often PublishTestResults, whose log
/// names no test.</para>
/// </summary>
public static class AzureReposBuildEvidence
{
    public static async Task<string> ExcerptAsync(
        BuildHttpClient builds, Guid projectId, int buildId, CiReadRequest request, CancellationToken ct,
        Func<CancellationToken, Task<TestManagementHttpClient>>? tests = null)
    {
        var timeline = await builds.GetBuildTimelineAsync(projectId, buildId, cancellationToken: ct);
        var failed = (timeline?.Records ?? []).Where(r => r.Result == TaskResult.Failed && r.RecordType == "Task").ToList();
        if (failed.Count == 0) return "The build failed; its timeline names no failed task.";
        var summary = failed.SelectMany(r => new[] { $"task failed: {r.Name}" }
            .Concat((r.Issues ?? []).Where(i => i.Type == IssueType.Error).Select(i => $"  {i.Message}")));
        var failedTests = tests is null ? null
            : await AzureReposTestEvidence.FailedAsync(tests, projectId, buildId, request.FailuresRead, ct);
        if (failedTests is { Count: > 0 }) return CiExcerpt.Of(failedTests.Concat(summary), null, request.ExcerptChars);
        var first = failed.MinBy(r => r.StartTime ?? DateTime.MaxValue)!;
        var log = first.Log is { } reference ? await LogTailAsync(builds, projectId, buildId, reference.Id, ct) : null;
        return CiExcerpt.Of(summary, log, request.ExcerptChars);
    }

    // The whole log is read and its end kept; Azure has no tail query. A log that cannot be read costs the tail only.
    private static async Task<string?> LogTailAsync(BuildHttpClient builds, Guid projectId, int buildId, int logId, CancellationToken ct)
    {
        try { return string.Join("\n", await builds.GetBuildLogLinesAsync(projectId, buildId, logId, cancellationToken: ct)); }
        catch (Exception ex) when (ex is not OperationCanceledException) { return $"(log not readable: {ex.Message})"; }
    }
}
