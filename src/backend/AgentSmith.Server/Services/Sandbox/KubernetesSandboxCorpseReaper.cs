using AgentSmith.Contracts.Sandbox;
using k8s;
using k8s.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Sandbox;

/// <summary>
/// p0355: the Kubernetes corpse-pod sweep. Deletes sandbox pods whose owning run is
/// not live — a CORPSE that would otherwise hold the namespace ResourceQuota and
/// starve new runs (19008Mi of 20Gi held by orphaned pods, so a fresh 4Gi sandbox was
/// forbidden and the run died mid-spawn). A live run is one whose DB lease heartbeat
/// is fresh (flush-proof) or that sits in the Redis active-runs set; a pod younger
/// than <see cref="MinPodAge"/> is spared. Runs periodically (leader housekeeping)
/// and at capacity-claim time.
///
/// p0465: the sweep asks only for the pods of ITS OWN liveness store
/// (<see cref="SandboxPodLabels.OwnedSelector"/>) — the namespace is shared, and a
/// second server in it used to delete the first one's live sandbox pods.
/// </summary>
public sealed class KubernetesSandboxCorpseReaper(
    IKubernetes client,
    KubernetesSandboxOptions options,
    SandboxPodLabels labels,
    LiveRunSetReader liveRuns,
    ILogger<KubernetesSandboxCorpseReaper> logger) : ISandboxCorpseReaper
{
    public static readonly TimeSpan MinPodAge = TimeSpan.FromSeconds(60);

    private bool _unownedSweepDone;

    public async Task<int> ReapCorpsesAsync(CancellationToken cancellationToken)
    {
        var pods = await ListCandidatesAsync(cancellationToken);
        if (pods.Count == 0) return 0;

        var live = await liveRuns.ReadAsync(cancellationToken);
        var reaped = 0;
        foreach (var (podName, runId) in SelectCorpses(pods, live, MinPodAge, DateTimeOffset.UtcNow))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (await DeleteAsync(podName, runId, cancellationToken)) reaped++;
        }
        return reaped;
    }

    // p0465: the owned selector is the steady state. A pod from a binary that predates
    // the owner stamp can never match it, so the FIRST sweep of the process also lists
    // the un-stamped pods — once, under the same rails.
    private async Task<IReadOnlyList<V1Pod>> ListCandidatesAsync(CancellationToken ct)
    {
        var pods = await ListAsync(labels.OwnedSelector, ct);
        if (_unownedSweepDone) return pods;
        _unownedSweepDone = true;
        var unowned = await ListAsync(SandboxPodLabels.UnownedSelector, ct);
        if (unowned.Count > 0)
            logger.LogInformation(
                "Corpse reaper one-time sweep: {Count} sandbox pod(s) carry no owner label "
                + "(created before p0465) and are judged on the age and live-run rails alone", unowned.Count);
        return [.. pods, .. unowned];
    }

    private async Task<IReadOnlyList<V1Pod>> ListAsync(string selector, CancellationToken ct)
    {
        try
        {
            var pods = await client.CoreV1.ListNamespacedPodAsync(
                options.Namespace, labelSelector: selector, cancellationToken: ct);
            return pods.Items is { Count: > 0 } items ? [.. items] : [];
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Corpse reaper could not list sandbox pods '{Selector}' in namespace {Ns} — skipping",
                selector, options.Namespace);
            return [];
        }
    }

    // Pure: given this store's sandbox pods and the live-run set, name the corpses
    // (pod name + its run-id label) to delete. Extracted so the corpse decision is
    // unit-tested without a k8s client mock. A pod is a corpse when it is older than
    // <paramref name="minAge"/> AND its run-id label maps to no live run (or carries
    // no run id at all — a runless probe pod its owner never cleaned up).
    internal static IReadOnlyList<(string PodName, string RunId)> SelectCorpses(
        IEnumerable<V1Pod> pods, ISet<string> liveRuns, TimeSpan minAge, DateTimeOffset now)
    {
        var corpses = new List<(string, string)>();
        foreach (var pod in pods)
        {
            var name = pod.Metadata?.Name;
            if (string.IsNullOrEmpty(name)) continue;
            var runId = LabelOrEmpty(pod, SandboxPodLabels.RunIdLabel);
            if (PodAge(pod, now) < minAge) continue;              // spawn-window race rail
            if (runId.Length > 0 && liveRuns.Contains(runId)) continue; // a live run owns it
            corpses.Add((name!, runId));
        }
        return corpses;
    }

    private async Task<bool> DeleteAsync(string podName, string runId, CancellationToken ct)
    {
        logger.LogInformation(
            "Corpse reaper DELETE pod {Pod} runId={RunId} — no live run owns it",
            podName, runId.Length > 0 ? runId : "—");
        try
        {
            await client.CoreV1.DeleteNamespacedPodAsync(
                podName, options.Namespace, gracePeriodSeconds: 0, cancellationToken: ct);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Corpse reaper failed to delete pod {Pod}", podName);
            return false;
        }
    }

    private static TimeSpan PodAge(V1Pod pod, DateTimeOffset now)
    {
        var created = pod.Metadata?.CreationTimestamp;
        if (created is null) return TimeSpan.MaxValue; // no timestamp → treat as old (reapable)
        return now - new DateTimeOffset(DateTime.SpecifyKind(created.Value, DateTimeKind.Utc));
    }

    private static string LabelOrEmpty(V1Pod pod, string key) =>
        pod.Metadata?.Labels is { } labels && labels.TryGetValue(key, out var v) ? v : string.Empty;
}
