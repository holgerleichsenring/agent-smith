using AgentSmith.Application.Models.Registry;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Registry;

/// <summary>
/// Coordinates the generic registry-auth path for one sandbox, AFTER the
/// deterministic NuGet/npm fast-paths: a present context.yaml
/// <c>registry_auth</c> section (operator-authored or persisted) is replayed
/// LLM-free and always wins; otherwise the bounded host-grep detects uncovered
/// hosts and ONE Scout-role LLM call emits the templated config, which is
/// guarded, substituted, written, and persisted once. Every failure surfaces
/// loudly per host; the run proceeds. Returns the number of files written.
/// </summary>
public sealed class GenericRegistryAuthApplier(
    RegistryAuthTemplateStore templateStore,
    RegistryHostGrep hostGrep,
    IRegistryAuthStager stager,
    StagedAuthFileWriter fileWriter,
    RegistryAuthFailureReporter failureReporter,
    AgentSmithConfig config,
    ILogger<GenericRegistryAuthApplier> logger)
{
    private const string WorkRoot = "/work";

    public async Task<int> ApplyAsync(
        string repoKey, ISandbox sandbox, ISandboxFileReader reader,
        IReadOnlyList<string> listing, ISet<string> coveredHosts,
        Func<AgentConfig> agentFactory, CancellationToken ct)
    {
        var declared = await templateStore.TryReadAsync(repoKey, listing, reader, ct);
        if (declared is not null)
        {
            var (written, _) = await WriteAllAsync(repoKey, reader, declared, ct);
            return written.Count;
        }
        return await StageViaLlmAsync(repoKey, sandbox, reader, listing, coveredHosts, agentFactory, ct);
    }

    private async Task<int> StageViaLlmAsync(
        string repoKey, ISandbox sandbox, ISandboxFileReader reader,
        IReadOnlyList<string> listing, ISet<string> coveredHosts,
        Func<AgentConfig> agentFactory, CancellationToken ct)
    {
        var uncovered = await hostGrep.FindUncoveredAsync(
            listing, coveredHosts, config.Registries, reader, repoKey, ct);
        if (uncovered.Count == 0) return 0;

        RegistryAuthStagingResult result;
        try
        {
            result = await stager.StageAsync(sandbox, WorkRoot, uncovered, agentFactory(), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "{Repo}: registry-auth stager LLM call failed.", repoKey);
            await ReportHostsAsync(repoKey, Hosts(uncovered), $"stager LLM call failed: {ex.Message}", ct);
            return 0;
        }

        var (written, handledHosts) = await WriteAllAsync(repoKey, reader, result.Files, ct);
        await ReportUnhandledHostsAsync(repoKey, uncovered, handledHosts, ct);
        if (written.Count > 0)
            await templateStore.PersistAsync(repoKey, listing, reader, written, ct);
        return written.Count;
    }

    // handledHosts = hosts either staged successfully or already covered by a loud
    // per-file failure report — so a host never produces two decision lines.
    private async Task<(IReadOnlyList<StagedAuthFile> Written, ISet<string> HandledHosts)> WriteAllAsync(
        string repoKey, ISandboxFileReader reader, IReadOnlyList<StagedAuthFile> files, CancellationToken ct)
    {
        var written = new List<StagedAuthFile>();
        var handledHosts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in files)
        {
            var outcome = await fileWriter.WriteAsync(repoKey, reader, file, ct);
            if (outcome.Written)
                written.Add(new StagedAuthFile(outcome.WrittenPath!, file.Content));
            else
                await ReportHostsAsync(repoKey, outcome.Hosts, outcome.FailureReason!, ct);
            handledHosts.UnionWith(outcome.Hosts);
        }
        return (written, handledHosts);
    }

    private async Task ReportUnhandledHostsAsync(
        string repoKey, IReadOnlyList<UncoveredRegistry> uncovered, ISet<string> handledHosts, CancellationToken ct)
    {
        var unhandled = Hosts(uncovered).Where(h => !handledHosts.Contains(h)).ToList();
        await ReportHostsAsync(repoKey, unhandled, "stager emitted no stageable config for this host", ct);
    }

    private async Task ReportHostsAsync(
        string repoKey, IReadOnlyList<string> hosts, string reason, CancellationToken ct)
    {
        foreach (var host in hosts)
            await failureReporter.ReportAsync(repoKey, host, reason, ct);
    }

    private static IReadOnlyList<string> Hosts(IReadOnlyList<UncoveredRegistry> uncovered) =>
        uncovered.Select(u => u.Registry.Host).ToList();
}
