using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Services.Events;
using k8s;
using k8s.Models;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace AgentSmith.Server.Services.Sandbox;

/// <summary>
/// p0355: the Kubernetes corpse-pod sweep. Lists pods labelled
/// <c>app=agentsmith-sandbox</c>, reads each pod's <see cref="PodSpecBuilder.RunIdLabel"/>,
/// and deletes any pod whose run is not live — a CORPSE that would otherwise hold
/// the namespace ResourceQuota and starve new runs (the real incident:
/// used 19008Mi of 20Gi held by orphaned pods, so a fresh 4Gi sandbox was forbidden
/// and the run was killed mid-spawn). A live run is one whose DB lease heartbeat is
/// fresh (flush-proof) or that sits in the Redis active-runs set. A pod younger than
/// <see cref="MinPodAge"/> is spared — its run id may not have entered the active set
/// yet (spawn-window race), mirroring the Docker orphan reaper's age rail. Runs both
/// periodically (leader housekeeping) and at capacity-claim time.
/// </summary>
public sealed class KubernetesSandboxCorpseReaper(
    IKubernetes client,
    KubernetesSandboxOptions options,
    IActiveRunLease activeRunLease,
    IConnectionMultiplexer redis,
    ILogger<KubernetesSandboxCorpseReaper> logger) : ISandboxCorpseReaper
{
    private const string LabelSelector = "app=" + PodSpecBuilder.AppLabel;
    public static readonly TimeSpan MinPodAge = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan LeaseFreshFor = TimeSpan.FromMinutes(3);

    public async Task<int> ReapCorpsesAsync(CancellationToken cancellationToken)
    {
        V1PodList pods;
        try
        {
            pods = await client.CoreV1.ListNamespacedPodAsync(
                options.Namespace, labelSelector: LabelSelector, cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Corpse reaper could not list sandbox pods in namespace {Ns} — skipping this sweep",
                options.Namespace);
            return 0;
        }
        if (pods.Items is not { Count: > 0 }) return 0;

        var live = await ReadLiveRunsAsync();
        var corpses = SelectCorpses(pods.Items, live, MinPodAge, DateTimeOffset.UtcNow);
        var reaped = 0;
        foreach (var (podName, runId) in corpses)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (await DeleteAsync(podName, runId, cancellationToken)) reaped++;
        }
        return reaped;
    }

    // Pure: given the namespace's sandbox pods and the live-run set, name the corpses
    // (pod name + its run-id label) to delete. Extracted so the corpse decision is
    // unit-tested without a k8s client mock. A pod is a corpse when it is older than
    // <paramref name="minAge"/> AND its run-id label maps to no live run (or carries
    // no run id at all — a pre-p0355 orphan with no owner signal).
    internal static IReadOnlyList<(string PodName, string RunId)> SelectCorpses(
        IEnumerable<V1Pod> pods, ISet<string> liveRuns, TimeSpan minAge, DateTimeOffset now)
    {
        var corpses = new List<(string, string)>();
        foreach (var pod in pods)
        {
            var name = pod.Metadata?.Name;
            if (string.IsNullOrEmpty(name)) continue;
            var runId = LabelOrEmpty(pod, PodSpecBuilder.RunIdLabel);
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

    private async Task<ISet<string>> ReadLiveRunsAsync()
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        try
        {
            var members = await redis.GetDatabase().SetMembersAsync(EventStreamKeys.ActiveRunsSet);
            foreach (var m in members) set.Add((string)m!);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Corpse reaper could not read the Redis active-runs set — DB lease only");
        }
        // Union the flush-proof DB lease: an empty/flushed Redis must not make a live
        // run's pod look like a corpse (the empty-Redis meltdown). A live run renews
        // its DB heartbeat, so its run id stays here even with Redis empty.
        foreach (var runId in await activeRunLease.GetActiveRunIdsAsync(LeaseFreshFor, CancellationToken.None))
            set.Add(runId);
        return set;
    }

    private static TimeSpan PodAge(V1Pod pod, DateTimeOffset now)
    {
        var created = pod.Metadata?.CreationTimestamp;
        if (created is null) return TimeSpan.MaxValue; // no timestamp → treat as old (reapable)
        var createdUtc = new DateTimeOffset(DateTime.SpecifyKind(created.Value, DateTimeKind.Utc));
        return now - createdUtc;
    }

    private static string LabelOrEmpty(V1Pod pod, string key) =>
        pod.Metadata?.Labels is { } labels && labels.TryGetValue(key, out var v) ? v : string.Empty;
}
