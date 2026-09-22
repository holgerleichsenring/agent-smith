using AgentSmith.Contracts.Sandbox;
using k8s;
using k8s.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Sandbox;

/// <summary>
/// p0355: the Kubernetes corpse-pod sweep. Deletes sandbox pods no rail saves — a
/// CORPSE that would otherwise hold the namespace ResourceQuota and starve new runs
/// (19008Mi of 20Gi held by orphaned pods, so a fresh 4Gi sandbox was forbidden and
/// the run died mid-spawn). Runs periodically (leader housekeeping) and at
/// capacity-claim time.
///
/// p0465: the sweep asks only for the pods of ITS OWN liveness store
/// (<see cref="SandboxPodLabels.OwnedSelector"/>) — the namespace is shared, and a
/// second server in it used to delete the first one's live sandbox pods.
///
/// 2026-09-22-2d11a: the rails moved into <see cref="SandboxReapJudge"/>, shared with
/// the Docker reaper, and a pod whose conversation is held is now one of them.
/// </summary>
public sealed class KubernetesSandboxCorpseReaper(
    IKubernetes client,
    KubernetesSandboxOptions options,
    SandboxPodLabels labels,
    LiveRunSetReader liveRuns,
    HeldConversationReader heldConversations,
    ILogger<KubernetesSandboxCorpseReaper> logger) : ISandboxCorpseReaper
{
    public static readonly TimeSpan MinPodAge = TimeSpan.FromSeconds(60);

    private bool _unownedSweepDone;

    public async Task<int> ReapCorpsesAsync(CancellationToken cancellationToken)
    {
        var pods = await ListCandidatesAsync(cancellationToken);
        if (pods.Count == 0) return 0;

        var candidates = SandboxPodCandidates.From(pods, DateTimeOffset.UtcNow);
        var live = await liveRuns.ReadAsync(cancellationToken);
        var held = await heldConversations.ReadAsync(candidates, cancellationToken);
        var reaped = 0;
        foreach (var corpse in SandboxReapJudge.Judge(candidates, live, held, MinPodAge)
                     .Where(v => v.Outcome == SandboxReapOutcome.Orphan))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (await DeleteAsync(corpse.SandboxId, corpse.RunId, cancellationToken)) reaped++;
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

    private async Task<bool> DeleteAsync(string podName, string runId, CancellationToken ct)
    {
        logger.LogInformation(
            "Corpse reaper DELETE pod {Pod} runId={RunId} — no live run and no live conversation owns it",
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
}
