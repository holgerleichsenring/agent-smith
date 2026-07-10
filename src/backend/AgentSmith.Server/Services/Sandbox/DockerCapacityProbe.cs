using AgentSmith.Contracts.Sandbox;
using Docker.DotNet;
using Docker.DotNet.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Sandbox;

/// <summary>
/// p0269a: Docker capacity guard. A Docker daemon without per-container limits
/// gives no create-time capacity signal — an over-subscribed host OOM-kills the
/// process LATER, indistinguishable from a crash. So capacity here is a
/// DETERMINISTIC configured cap: admit while the run's sandboxes still fit under
/// <see cref="DockerSandboxOptions.MaxConcurrentSandboxes"/> running sandbox
/// containers (counted by the distinct job-id label). A cap of 0 means unbounded
/// (admit always) — the historic behaviour. The resource quantities are ignored;
/// Docker capacity is expressed as a count, not a cpu/memory budget.
/// </summary>
public sealed class DockerCapacityProbe(
    IDockerClient docker,
    DockerSandboxOptions options,
    ILogger<DockerCapacityProbe> logger) : ISandboxCapacityProbe
{
    public async Task<CapacityDecision> HasCapacityAsync(RunFootprint footprint, CancellationToken cancellationToken)
    {
        var cap = options.MaxConcurrentSandboxes;
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
        // the cap mid-run.
        var needed = footprint.Sandboxes.Count;
        if (running + needed <= cap) return CapacityDecision.Admit();
        return CapacityDecision.Deny(
            $"Docker host at the concurrent-sandbox cap ({running} running + {needed} needed > {cap}); "
            + "waiting for slots to free.");
    }

    private async Task<int> CountRunningSandboxesAsync(CancellationToken ct)
    {
        var parameters = new ContainersListParameters
        {
            All = false, // running only — those are the ones consuming host resources
            Filters = new Dictionary<string, IDictionary<string, bool>>
            {
                ["label"] = new Dictionary<string, bool> { [DockerContainerSpecBuilder.JobIdLabel] = true }
            }
        };
        var containers = await docker.Containers.ListContainersAsync(parameters, ct);
        return containers
            .Select(c => LabelOrEmpty(c.Labels, DockerContainerSpecBuilder.JobIdLabel))
            .Where(v => !string.IsNullOrEmpty(v))
            .Distinct(StringComparer.Ordinal)
            .Count();
    }

    private static string LabelOrEmpty(IDictionary<string, string>? labels, string key) =>
        labels is not null && labels.TryGetValue(key, out var v) ? v : string.Empty;
}
