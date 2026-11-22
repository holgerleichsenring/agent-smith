namespace AgentSmith.Infrastructure.Services.RateLimiting;

/// <summary>
/// 2026-11-22-ab45: which runs were already told about a deployment, so a notice is said once per run and deployment.
/// Bounded: per deployment only the most recent runs are kept, so runs that take turns on one deployment are still
/// told once each, and a long-lived server does not collect every run it ever served.
/// </summary>
internal sealed class RecentlyToldRuns
{
    private const int RunsKept = 32; // not a limit: how many recent runs per deployment are remembered as told

    private readonly Dictionary<string, Queue<string>> _told = new(StringComparer.Ordinal);

    /// <summary>True the first time <paramref name="runId"/> is told about <paramref name="deployment"/>.</summary>
    public bool FirstTime(string deployment, string runId)
    {
        lock (_told)
        {
            if (!_told.TryGetValue(deployment, out var runs)) _told[deployment] = runs = new Queue<string>();
            if (runs.Contains(runId)) return false;
            runs.Enqueue(runId);
            if (runs.Count > RunsKept) runs.Dequeue();
            return true;
        }
    }
}
