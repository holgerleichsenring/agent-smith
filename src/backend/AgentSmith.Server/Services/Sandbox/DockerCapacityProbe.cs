using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using Docker.DotNet;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Sandbox;

/// <summary>
/// p0269a: Docker capacity guard. A Docker daemon without per-container limits
/// gives no create-time capacity signal — an over-subscribed host OOM-kills the
/// process LATER, indistinguishable from a crash. So capacity here is a
/// DETERMINISTIC configured bound: admit while the run's sandboxes still fit under
/// the concurrent-sandbox bound (counted by the distinct job-id label). A bound of
/// 0 means unbounded (admit always) — the historic behaviour. The resource
/// quantities are ignored; Docker capacity is expressed as a count, not a
/// cpu/memory budget.
///
/// p0465: only THIS liveness store's sandboxes are counted. The bound is a small
/// number (default 2), so counting a foreign server's containers replaces one
/// instance killing another with one instance starving another.
///
/// 2026-09-18-0f27: the bound is resolved through the configuration loader AT THE
/// MOMENT OF THE DECISION — the stored sandbox settings first, then
/// <c>SANDBOX_MAX_CONCURRENT</c>, then the built-in default — so changing it in the
/// catalog reaches the next decision without a restart.
/// </summary>
public sealed class DockerCapacityProbe(
    IDockerClient docker,
    DockerSandboxQuery query,
    IConfigurationLoader configLoader,
    ServerContext serverContext,
    ILogger<DockerCapacityProbe> logger) : ISandboxCapacityProbe
{
    /// <summary>The empty-store fallback: read only when the catalog names no bound.</summary>
    public const string BoundEnvironmentVariable = "SANDBOX_MAX_CONCURRENT";

    /// <summary>A conservative single-host floor, used when nothing else names a bound.</summary>
    public const int DefaultBound = 2;

    // Instance state on the singleton probe: the last resolution reported, so a
    // steady bound is logged once instead of on every decision.
    private ConcurrentSandboxBound? _reported;

    public async Task<CapacityDecision> HasCapacityAsync(RunFootprint footprint, CancellationToken cancellationToken)
    {
        var cap = ReportedBound().Value;
        if (cap <= 0) return CapacityDecision.Admit();

        int running;
        try
        {
            running = await CountRunningSandboxesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            // Fail-open: a daemon read failure must not block every run. The spawn
            // itself remains the hard guard.
            logger.LogWarning(ex, "Docker capacity probe failed to list containers — admitting");
            return CapacityDecision.Admit();
        }

        // p0320b: the run needs a slot for EVERY sandbox it will spawn (one per
        // repo) — admitting on a single free slot let multi-repo runs crash into
        // the bound mid-run.
        var needed = footprint.Sandboxes.Count;
        if (running + needed <= cap) return CapacityDecision.Admit();
        return CapacityDecision.Deny(
            $"Docker host at the concurrent-sandbox cap ({running} running + {needed} needed > {cap}); "
            + "waiting for slots to free.");
    }

    private ConcurrentSandboxBound ReportedBound()
    {
        var bound = ResolveBound();
        if (bound == _reported) return bound;
        _reported = bound;
        logger.LogInformation(
            "Concurrent-sandbox bound resolved to {Bound} from {BoundSource}", bound.Value, bound.Source);
        return bound;
    }

    private ConcurrentSandboxBound ResolveBound()
    {
        var configured = configLoader.LoadConfig(serverContext.ConfigPath).Sandbox.MaxConcurrentSandboxes;
        if (configured.HasValue) return new ConcurrentSandboxBound(configured.Value, "the sandbox settings");
        return int.TryParse(Environment.GetEnvironmentVariable(BoundEnvironmentVariable), out var fromEnvironment)
            ? new ConcurrentSandboxBound(fromEnvironment, BoundEnvironmentVariable)
            : new ConcurrentSandboxBound(DefaultBound, "the built-in default");
    }

    private async Task<int> CountRunningSandboxesAsync(CancellationToken ct)
    {
        // includeStopped: false — a running sandbox is what consumes host resources.
        var containers = await docker.Containers.ListContainersAsync(query.Owned(includeStopped: false), ct);
        return containers
            .Select(c => LabelOrEmpty(c.Labels, DockerContainerSpecBuilder.JobIdLabel))
            .Where(v => !string.IsNullOrEmpty(v))
            .Distinct(StringComparer.Ordinal)
            .Count();
    }

    private static string LabelOrEmpty(IDictionary<string, string>? labels, string key) =>
        labels is not null && labels.TryGetValue(key, out var v) ? v : string.Empty;
}
