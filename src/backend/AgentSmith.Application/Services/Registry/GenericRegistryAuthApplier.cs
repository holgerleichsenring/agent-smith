using AgentSmith.Application.Models.Registry;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Registry;

/// <summary>
/// Coordinates the generic (LLM-fallback) registry-auth path for one sandbox:
/// detect uncovered ecosystems, ask the stager for templated global config,
/// reject leaked secrets and out-of-scope writes, substitute the real token
/// host-side, and write. Invoked by <c>SetupRegistryAuthHandler</c> AFTER the
/// deterministic NuGet/npm fast-paths; returns the number of files written.
/// </summary>
public sealed class GenericRegistryAuthApplier(
    UncoveredEcosystemScanner scanner,
    IRegistryAuthStager stager,
    RegistryTokenSubstitutor substitutor,
    SecretLeakGuard leakGuard,
    AgentSmithConfig config,
    ILogger<GenericRegistryAuthApplier> logger)
{
    private const string WorkRoot = "/work";

    public async Task<int> ApplyAsync(
        string repoKey, ISandbox sandbox, ISandboxFileReader reader,
        IReadOnlyList<string> listing, ISet<string> coveredHosts,
        Func<AgentConfig> agentFactory, CancellationToken ct)
    {
        var uncovered = await scanner.ScanAsync(
            listing, coveredHosts, config.Registries, reader, repoKey, ct);
        if (uncovered.Count == 0) return 0;

        var result = await stager.StageAsync(sandbox, WorkRoot, uncovered, agentFactory(), ct);
        var staged = 0;
        foreach (var file in result.Files)
            if (await TryStageFileAsync(repoKey, reader, file, ct)) staged++;

        logger.LogInformation(
            "{Repo}: generically staged {Staged}/{Emitted} auth file(s) for host(s) [{Hosts}].",
            repoKey, staged, result.Files.Count, string.Join(", ", result.TargetedHosts));
        return staged;
    }

    private async Task<bool> TryStageFileAsync(
        string repoKey, ISandboxFileReader reader, StagedAuthFile file, CancellationToken ct)
    {
        if (!IsUserConfigScope(file.Path))
        {
            logger.LogWarning(
                "{Repo}: rejected staged auth file '{Path}' — outside the user-config scope (would write into the repo tree).",
                repoKey, file.Path);
            return false;
        }

        if (!leakGuard.IsClean(file.Content, config.Registries))
        {
            logger.LogWarning(
                "{Repo}: rejected staged auth file '{Path}' — its content contained a real token instead of the placeholder.",
                repoKey, file.Path);
            return false;
        }

        var substitution = substitutor.Substitute(file.Content, config.Registries);
        if (!substitution.IsSuccess)
        {
            logger.LogWarning(
                "{Repo}: rejected staged auth file '{Path}' — {Reason}.", repoKey, file.Path, substitution.FailureReason);
            return false;
        }

        await reader.WriteAsync(file.Path, substitution.Content!, ct);
        logger.LogInformation("{Repo}: staged generic registry auth at {Path}.", repoKey, file.Path);
        return true;
    }

    private static bool IsUserConfigScope(string path) =>
        path.StartsWith('/')
        && !path.Equals(WorkRoot, StringComparison.Ordinal)
        && !path.StartsWith(WorkRoot + "/", StringComparison.Ordinal);
}
