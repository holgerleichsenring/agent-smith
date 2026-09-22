using Docker.DotNet;
using Docker.DotNet.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Sandbox;

/// <summary>
/// p0201: process-wide singleton that walks the sandbox containers of THIS liveness
/// store every <see cref="ScanInterval"/> and force-removes those no rail saves. The
/// age rail closes the spawn-window race (label visible before the run-id enters the
/// active set); the live-run rail catches the steady-state orphan; 2026-09-22-2d11a
/// added the held-conversation rail. The rails themselves live in
/// <see cref="SandboxReapJudge"/>, shared with the Kubernetes corpse sweep.
///
/// p0465: the ownership term is part of the QUERY, not of the decision — a container
/// spawned against a different Redis store is never listed, so no rail has to save it.
/// Deriving ownership from the active-run set is what let a second server on the same
/// daemon delete the first one's live sandboxes.
/// </summary>
public sealed class SandboxOrphanReaper(
    IDockerClient docker,
    DockerSandboxQuery query,
    LiveRunSetReader liveRuns,
    HeldConversationReader heldConversations,
    DockerSandboxRemover remover,
    ILogger<SandboxOrphanReaper> logger) : BackgroundService
{
    public static readonly TimeSpan ScanInterval = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan MinContainerAge = TimeSpan.FromSeconds(60);

    private bool _unownedSweepDone;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "SandboxOrphanReaper started (scan={Scan}, min-age={MinAge})", ScanInterval, MinContainerAge);
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await ScanOnceAsync(stoppingToken); }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { logger.LogError(ex, "SandboxOrphanReaper scan failed"); }
            try { await Task.Delay(ScanInterval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    // Public for the docker-tier harness; lets a test drive a single scan
    // deterministically rather than waiting on the 30s timer.
    public async Task ScanOnceAsync(CancellationToken ct)
    {
        var containers = await ListCandidatesAsync(ct);
        if (containers.Count == 0) return;
        var candidates = SandboxContainerCandidates.From(containers, DateTimeOffset.UtcNow);
        var live = await liveRuns.ReadAsync(ct);
        var held = await heldConversations.ReadAsync(candidates, ct);
        foreach (var verdict in SandboxReapJudge.Judge(candidates, live, held, MinContainerAge))
        {
            ct.ThrowIfCancellationRequested();
            Log(verdict);
            if (verdict.Outcome == SandboxReapOutcome.Orphan)
                await remover.RemoveAsync(verdict.SandboxId, verdict.JobId, ct);
        }
    }

    // p0465: the owned query is the steady state. A container spawned by a binary that
    // predates the owner stamp can never appear in it, so the FIRST scan of the process
    // also sweeps the un-stamped ones — once, under the same rails. That is the k8s
    // corpse reaper's existing treatment of a pod with no owner signal.
    private async Task<IList<ContainerListResponse>> ListCandidatesAsync(CancellationToken ct)
    {
        var owned = await docker.Containers.ListContainersAsync(query.Owned(includeStopped: true), ct);
        if (_unownedSweepDone) return owned;
        _unownedSweepDone = true;
        var unowned = (await docker.Containers.ListContainersAsync(query.AnyOwner(includeStopped: true), ct))
            .Where(DockerSandboxQuery.IsUnowned).ToList();
        if (unowned.Count > 0)
            logger.LogInformation(
                "Reaper one-time sweep: {Count} sandbox container(s) carry no owner label (spawned "
                + "before p0465) and are judged on the age and live-run rails alone", unowned.Count);
        return [.. owned, .. unowned];
    }

    private void Log(SandboxReapVerdict v)
    {
        const string line =
            "Reaper {Outcome}: container {Id} jobId={JobId} runId={RunId} conversation={Conversation} age={Age:F1}s";
        var id = ShortId(v.SandboxId);
        var conversation = v.ConversationId.Length > 0 ? v.ConversationId : "—";
        if (v.Outcome == SandboxReapOutcome.Orphan)
            logger.LogInformation(line, "REMOVE", id, v.JobId, v.RunId, conversation, v.Age.TotalSeconds);
        else
            logger.LogDebug(line, "SKIP " + v.Outcome, id, v.JobId, v.RunId, conversation, v.Age.TotalSeconds);
    }

    private static string ShortId(string id) => id.Length > 12 ? id[..12] : id;
}
