using AgentSmith.Application.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Commands;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// p0336: builds the run's full footprint by reading every repo's context.yaml
/// remotely (the p0331 inventory: repo → contexts). The pipeline runs in the server, so
/// the footprint is the sandboxes alone.
/// 2026-11-25-9992b: the run is ONE workspace pod with a container per toolchain image across all its repositories,
/// each at the largest resource envelope any of its contexts asks for — the same grouping the coordinator spawns — so
/// the reserved footprint is the sum of those containers (plus the browser pod, which stays its own).
/// </summary>
public sealed class RunFootprintCalculator(
    ISandboxLanguageResolver languageResolver,
    ISandboxResourceResolver resourceResolver,
    ILogger<RunFootprintCalculator> logger,
    IOptions<SandboxGlobalConfig> sandbox) : IRunFootprintCalculator
{
    internal const string BrowserPodName = "browser";

    public async Task<RunFootprintBreakdown> CalculateAsync(
        ResolvedProject project, string? pipelineName, ContextReadBranches branches, CancellationToken ct)
    {
        var contexts = new List<(string Repo, RemoteContextDiscovery Discovery)>();
        foreach (var repo in project.Repos)
            contexts.AddRange((await languageResolver.ResolveAllAsync(repo, branches, ct)).Select(d => (repo.Name ?? "?", d)));
        var pods = contexts.GroupBy(c => ImageOf(c.Discovery), StringComparer.Ordinal)
            .Select(group => ContainerForImage(project, pipelineName, group.Key, [.. group]))
            .ToList();
        if (BrowserPod(project, pipelineName) is { } browser) pods.Add(browser);

        return Totalize(pods, project);
    }

    // One container of the workspace: the repositories it serves, their contexts, the image and the largest envelope.
    private RunFootprintPod ContainerForImage(
        ResolvedProject project, string? pipeline, string image,
        IReadOnlyList<(string Repo, RemoteContextDiscovery Discovery)> group)
    {
        var limits = ResourceEnvelope.Max(
            group.Select(c => resourceResolver.Resolve(project, pipeline, c.Discovery.Resources)));
        return new RunFootprintPod(
            string.Join('+', group.Select(c => c.Repo).Distinct(StringComparer.Ordinal)),
            [.. group.Select(c => c.Discovery.ContextName)], image, limits.CpuLimit, limits.MemoryLimit);
    }

    // 2026-10-01-283df: a project whose runs may render gets the browser pod reserved at the browser
    // profile — only for a pipeline whose master writes code, the one surface render_reference joins.
    private RunFootprintPod? BrowserPod(ResolvedProject project, string? pipeline) =>
        project.Sandbox?.Browser?.Enabled == true && (pipeline is null || PipelinePresets.ExpectsCodeChanges(pipeline))
            ? new RunFootprintPod(BrowserPodName, [], BrowserPodName,
                sandbox.Value.Browser.Resources.CpuLimit, sandbox.Value.Browser.Resources.MemoryLimit)
            : null;

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
            "Footprint for {Project}: {Pods} container(s), cpu {Cpu}, memory {Mem}",
            project.Name, pods.Count, FormatCpu(cpu), FormatMem(mem));
        return new RunFootprintBreakdown(
            pods, FormatCpu(cpu), FormatMem(mem), cpu, mem, [], SummaryOf(pods));
    }

    private static string SummaryOf(IReadOnlyList<RunFootprintPod> pods) =>
        $"1 workspace pod, {pods.Count(p => p.Contexts.Count > 0)} container(s): " + string.Join(", ", pods.Select(p =>
            p.Contexts.Count == 0 ? p.Repo : $"{p.Repo}/{string.Join('+', p.Contexts)}"));

    private static string FormatCpu(long nanoCpus) => (nanoCpus / 1_000_000_000.0).ToString("0.##");

    private static string FormatMem(long bytes) => (bytes / (1024.0 * 1024 * 1024)).ToString("0.#") + "Gi";
}
