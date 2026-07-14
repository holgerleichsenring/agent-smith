using AgentSmith.Application.Services.Orchestrator;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// p0336: builds the run's full footprint by reading every repo's context.yaml
/// remotely (the p0331 inventory: repo → contexts), sizing each toolchain-group
/// sandbox via <see cref="ISandboxResourceResolver"/> at its RESOLVED limit, and
/// adding the orchestrator pod. The DAP 3-repo case correctly yields 4 sandboxes
/// (Server splits sdk8/sdk9), not 3 — real toolchain groups, not a repo count.
/// </summary>
public sealed class RunFootprintCalculator(
    ISandboxLanguageResolver languageResolver,
    ISandboxResourceResolver resourceResolver,
    IOrchestratorResourceResolver orchestratorResolver,
    ILogger<RunFootprintCalculator> logger) : IRunFootprintCalculator
{
    public async Task<RunFootprintBreakdown> CalculateAsync(
        ResolvedProject project, string? pipelineName, CancellationToken ct)
    {
        var pods = new List<RunFootprintPod>();
        foreach (var repo in project.Repos)
            foreach (var discovery in await languageResolver.ResolveAllAsync(repo, ct))
                pods.Add(PodFor(project, pipelineName, repo.Name ?? "?", discovery));

        var orchestrator = orchestratorResolver.Resolve(project);
        if (orchestrator is not null)
            pods.Add(new RunFootprintPod(
                "orchestrator", [], "orchestrator", orchestrator.CpuLimit, orchestrator.MemoryLimit));

        return Totalize(pods, project);
    }

    private RunFootprintPod PodFor(
        ResolvedProject project, string? pipeline, string repoName, RemoteContextDiscovery discovery)
    {
        var limits = resourceResolver.Resolve(project, pipeline, discovery.Resources);
        var image = discovery.ToolchainImage ?? discovery.Language ?? "default";
        return new RunFootprintPod(repoName, [discovery.ContextName], image, limits.CpuLimit, limits.MemoryLimit);
    }

    private RunFootprintBreakdown Totalize(IReadOnlyList<RunFootprintPod> pods, ResolvedProject project)
    {
        long cpu = 0, mem = 0;
        foreach (var pod in pods)
        {
            if (KubernetesQuantity.TryParseCpuToNanoCpus(pod.CpuLimit, out var c)) cpu += c;
            if (KubernetesQuantity.TryParseMemoryToBytes(pod.MemLimit, out var m)) mem += m;
        }
        logger.LogInformation(
            "Footprint for {Project}: {Pods} pod(s), cpu {Cpu}, memory {Mem}",
            project.Name, pods.Count, FormatCpu(cpu), FormatMem(mem));
        return new RunFootprintBreakdown(
            pods, FormatCpu(cpu), FormatMem(mem), cpu, mem, [], SummaryOf(pods));
    }

    private static string SummaryOf(IReadOnlyList<RunFootprintPod> pods) =>
        $"{pods.Count} pod(s): " + string.Join(", ", pods.Select(p =>
            p.Contexts.Count == 0 ? p.Repo : $"{p.Repo}/{string.Join('+', p.Contexts)}"));

    private static string FormatCpu(long nanoCpus) => (nanoCpus / 1_000_000_000.0).ToString("0.##");

    private static string FormatMem(long bytes) => (bytes / (1024.0 * 1024 * 1024)).ToString("0.#") + "Gi";
}
