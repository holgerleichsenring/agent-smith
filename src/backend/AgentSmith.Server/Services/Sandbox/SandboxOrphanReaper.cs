using AgentSmith.Infrastructure.Services.Events;
using Docker.DotNet;
using Docker.DotNet.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace AgentSmith.Server.Services.Sandbox;

/// <summary>
/// p0201: process-wide singleton that walks Docker containers labelled
/// <see cref="DockerContainerSpecBuilder.JobIdLabel"/> every
/// <see cref="ScanInterval"/> and force-removes those that are BOTH older than
/// <see cref="MinContainerAge"/> AND not present in
/// <see cref="EventStreamKeys.ActiveRunsSet"/>. Two-rail safety: the age rail
/// closes the spawn-window race (label visible before the run-id enters the
/// active set); the active-set rail catches the steady-state orphan.
/// </summary>
public sealed class SandboxOrphanReaper(
    IDockerClient docker,
    IConnectionMultiplexer multiplexer,
    ILogger<SandboxOrphanReaper> logger) : BackgroundService
{
    public static readonly TimeSpan ScanInterval = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan MinContainerAge = TimeSpan.FromSeconds(60);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "SandboxOrphanReaper started (scan={Scan}, min-age={MinAge})",
            ScanInterval, MinContainerAge);
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
        var containers = await ListLabelledAsync(ct);
        if (containers.Count == 0) return;
        var activeRuns = await ReadActiveRunsAsync();
        foreach (var c in containers)
        {
            ct.ThrowIfCancellationRequested();
            await ConsiderAsync(c, activeRuns, ct);
        }
    }

    private async Task<IList<ContainerListResponse>> ListLabelledAsync(CancellationToken ct)
    {
        var parameters = new ContainersListParameters
        {
            All = true,
            Filters = new Dictionary<string, IDictionary<string, bool>>
            {
                ["label"] = new Dictionary<string, bool> { [DockerContainerSpecBuilder.JobIdLabel] = true }
            }
        };
        return await docker.Containers.ListContainersAsync(parameters, ct);
    }

    private async Task<HashSet<string>> ReadActiveRunsAsync()
    {
        var members = await multiplexer.GetDatabase().SetMembersAsync(EventStreamKeys.ActiveRunsSet);
        return new HashSet<string>(members.Select(m => (string)m!), StringComparer.Ordinal);
    }

    private async Task ConsiderAsync(ContainerListResponse container, HashSet<string> activeRuns, CancellationToken ct)
    {
        var jobId = LabelOrEmpty(container.Labels, DockerContainerSpecBuilder.JobIdLabel);
        var runId = LabelOrEmpty(container.Labels, DockerContainerSpecBuilder.RunIdLabel);
        var age = DateTimeOffset.UtcNow - new DateTimeOffset(container.Created, TimeSpan.Zero);
        if (age < MinContainerAge)
        {
            logger.LogDebug(
                "Reaper SKIP age-rail: container {Id} jobId={JobId} runId={RunId} age={Age:F1}s",
                ShortId(container.ID), jobId, runId, age.TotalSeconds);
            return;
        }
        if (!string.IsNullOrEmpty(runId) && activeRuns.Contains(runId))
        {
            logger.LogDebug(
                "Reaper SKIP active-run: container {Id} jobId={JobId} runId={RunId} age={Age:F1}s",
                ShortId(container.ID), jobId, runId, age.TotalSeconds);
            return;
        }
        await ReapAsync(container, jobId, runId, age, ct);
    }

    private async Task ReapAsync(ContainerListResponse container, string jobId, string runId, TimeSpan age, CancellationToken ct)
    {
        logger.LogInformation(
            "Reaper REMOVE: container {Id} jobId={JobId} runId={RunId} age={Age:F1}s",
            ShortId(container.ID), jobId, runId, age.TotalSeconds);
        try
        {
            await docker.Containers.RemoveContainerAsync(container.ID,
                new ContainerRemoveParameters { Force = true, RemoveVolumes = false }, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Reaper failed to remove orphan container {Id}", ShortId(container.ID));
            return;
        }
        await RemoveLabelledVolumesAsync(jobId, ct);
    }

    private async Task RemoveLabelledVolumesAsync(string jobId, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(jobId)) return;
        var slug = jobId.Length > 12 ? jobId[..12] : jobId;
        foreach (var name in new[] { $"agentsmith-sandbox-{slug}-shared", $"agentsmith-sandbox-{slug}-work" })
        {
            try { await docker.Volumes.RemoveAsync(name, force: true, ct); }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Reaper failed to remove volume {Name}", name);
            }
        }
    }

    private static string LabelOrEmpty(IDictionary<string, string>? labels, string key) =>
        labels is not null && labels.TryGetValue(key, out var v) ? v : string.Empty;

    private static string ShortId(string id) => id.Length > 12 ? id[..12] : id;
}
