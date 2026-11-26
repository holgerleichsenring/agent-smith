using AgentSmith.Contracts.Reviews;
using Microsoft.TeamFoundation.TestManagement.WebApi;

namespace AgentSmith.Infrastructure.Services.Providers.Source;

/// <summary>
/// 2026-11-26-49d3a: a failed build's failed tests as the host recorded them — the test runs published for the build
/// (_apis/test/runs?buildUri=…) and each run's failed results (failed, error, timeout, aborted; at most
/// merge.ci_failed_checks_read per run): test name, error message and stack. A test read the host refuses (no test scope)
/// or that times out answers null, so the caller falls back to the log tail.
/// </summary>
public static class AzureReposTestEvidence
{
    public static async Task<IReadOnlyList<string>?> FailedAsync(
        Func<CancellationToken, Task<TestManagementHttpClient>> client, Guid projectId, int buildId, int top, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(client);
        try
        {
            var tests = await client(ct);
            var runs = await tests.GetTestRunsAsync(projectId, buildUri: $"vstfs:///Build/Build/{buildId}", cancellationToken: ct);
            TestOutcome[] outcomes = [TestOutcome.Failed, TestOutcome.Error, TestOutcome.Timeout, TestOutcome.Aborted];
            var failed = new List<CiFailedTest>();
            foreach (var run in (runs ?? []).Where(r => r.UnanalyzedTests > 0 || r.TotalTests > r.PassedTests + r.NotApplicableTests))
            {
                var results = await tests.GetTestResultsAsync(projectId, run.Id, top: Math.Max(1, top), outcomes: outcomes, cancellationToken: ct);
                failed.AddRange((results ?? []).Select(r => new CiFailedTest(r.Outcome?.ToLowerInvariant() ?? "failed",
                    r.AutomatedTestName ?? r.TestCaseTitle, r.ErrorMessage, r.StackTrace)));
            }
            return CiTestLines.Of(failed);
        }
        catch (Exception) when (!ct.IsCancellationRequested) { return null; }
    }
}
