using Docker.DotNet;
using Docker.DotNet.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Sandbox;

/// <summary>
/// p0201: process-wide singleton that walks the sandbox containers of THIS liveness
/// store every <see cref="ScanInterval"/> and force-removes those that are BOTH older
/// than <see cref="MinContainerAge"/> AND owned by no live run. Two-rail safety: the
/// age rail closes the spawn-window race (label visible before the run-id enters the
/// active set); the live-run rail catches the steady-state orphan.
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
        var live = await liveRuns.ReadAsync(ct);
        foreach (var verdict in Judge(containers, live, MinContainerAge, DateTimeOffset.UtcNow))
        {
            ct.ThrowIfCancellationRequested();
            Log(verdict);
            if (verdict.Outcome == SandboxReapOutcome.Orphan)
                await remover.RemoveAsync(verdict.ContainerId, verdict.JobId, ct);
        }
    }

    // p0465: the owned query is the steady state. A container spawned by a binary that
    // predates the owner stamp can never appear in it, so the FIRST scan of the process
    // also sweeps the un-stamped ones — once, under the same two rails. That is the k8s
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

    // Pure: given this store's sandbox containers and the live-run set, name the
    // outcome for each. Extracted so both rails are unit-tested without a Docker mock.
    internal static IReadOnlyList<SandboxReapVerdict> Judge(
        IEnumerable<ContainerListResponse> containers, ISet<string> liveRuns,
        TimeSpan minAge, DateTimeOffset now)
    {
        var verdicts = new List<SandboxReapVerdict>();
        foreach (var container in containers)
        {
            var runId = LabelOrEmpty(container.Labels, DockerContainerSpecBuilder.RunIdLabel);
            var age = now - new DateTimeOffset(container.Created, TimeSpan.Zero);
            var outcome = age < minAge ? SandboxReapOutcome.TooYoung
                : runId.Length > 0 && liveRuns.Contains(runId) ? SandboxReapOutcome.RunIsLive
                : SandboxReapOutcome.Orphan;
            verdicts.Add(new SandboxReapVerdict(
                container.ID,
                LabelOrEmpty(container.Labels, DockerContainerSpecBuilder.JobIdLabel),
                runId, age, outcome));
        }
        return verdicts;
    }

    private void Log(SandboxReapVerdict v)
    {
        const string line = "Reaper {Outcome}: container {Id} jobId={JobId} runId={RunId} age={Age:F1}s";
        var id = ShortId(v.ContainerId);
        if (v.Outcome == SandboxReapOutcome.Orphan)
            logger.LogInformation(line, "REMOVE", id, v.JobId, v.RunId, v.Age.TotalSeconds);
        else
            logger.LogDebug(line, "SKIP " + v.Outcome, id, v.JobId, v.RunId, v.Age.TotalSeconds);
    }

    private static string LabelOrEmpty(IDictionary<string, string>? labels, string key) =>
        labels is not null && labels.TryGetValue(key, out var v) ? v : string.Empty;

    private static string ShortId(string id) => id.Length > 12 ? id[..12] : id;
}
