using AgentSmith.Application.Services.Orchestrator;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// p0336: builds the run's full footprint by reading every repo's context.yaml
/// remotely (the p0331 inventory: repo → contexts) and adding the orchestrator pod.
/// p0336c: sizes ONE pod per (repo, toolchain image) at the max resource envelope —
/// the SAME grouping the coordinator spawns — so the reserved footprint equals the
/// pods that actually run (was one-per-context, which over-reserved). A mixed-SDK
/// repo still yields one pod per image.
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
        {
            var discoveries = await languageResolver.ResolveAllAsync(repo, ct);
            foreach (var group in discoveries.GroupBy(ImageOf, StringComparer.Ordinal))
                pods.Add(PodForGroup(project, pipelineName, repo.Name ?? "?", group.ToList()));
        }

        var orchestrator = orchestratorResolver.Resolve(project);
        if (orchestrator is not null)
            pods.Add(new RunFootprintPod(
                "orchestrator", [], "orchestrator", orchestrator.CpuLimit, orchestrator.MemoryLimit));

        return Totalize(pods, project);
    }

    private RunFootprintPod PodForGroup(
        ResolvedProject project, string? pipeline, string repoName,
        IReadOnlyList<RemoteContextDiscovery> group)
    {
        var limits = ResourceEnvelope.Max(
            group.Select(d => resourceResolver.Resolve(project, pipeline, d.Resources)));
        return new RunFootprintPod(
            repoName, group.Select(d => d.ContextName).ToList(),
            ImageOf(group[0]), limits.CpuLimit, limits.MemoryLimit);
    }

    private static string ImageOf(RemoteContextDiscovery discovery) =>
        discovery.ToolchainImage ?? discovery.Language ?? "default";

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
