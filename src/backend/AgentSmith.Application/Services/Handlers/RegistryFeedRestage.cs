using System.Text.RegularExpressions;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// 2026-11-26-49d3b: before a package command (dotnet, nuget, msbuild, npm, npx, pnpm, yarn, make or a shell script) the
/// sandbox's NuGet and npm feeds are staged again from the repo configs as they are now, so a source the run added since
/// checkout — a second feed on a configured host — has its credential. NuGet keys a credential by source name only, so the
/// staging, not a host-wide entry, is what covers it. Staging writes only the user-level files under /root. A re-stage that
/// fails is logged; the command runs either way.
/// </summary>
public sealed partial class RegistryFeedRestage(MidRunRegistryAuth registryAuth, ILogger<RegistryFeedRestage> logger)
{
    [GeneratedRegex(@"(^|[\s;&|(/""'])(dotnet|nuget|msbuild|npm|npx|pnpm|yarn|make)([\s;&|)""']|$)|\.sh([\s;&|)""']|$)")]
    private static partial Regex PackageCommand();

    /// <summary>The command may restore packages.</summary>
    public static bool Restores(string? command) => command is not null && PackageCommand().IsMatch(command);

    public async Task BeforeCommandAsync(
        PipelineContext pipeline, string repoKey, string command, ISandbox sandbox, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        if (Restores(command)) await StageAsync(pipeline, repoKey, sandbox, cancellationToken);
    }

    /// <summary>Stages the sandbox's NuGet and npm feeds again, whatever runs next — a declared verify stage restores in
    /// its own sandbox however its command is written.</summary>
    public Task StageAsync(PipelineContext pipeline, string repoKey, ISandbox sandbox, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        return TolerantAsync(repoKey, () => registryAuth.StageAsync(pipeline, repoKey, sandbox, cancellationToken, generic: false),
            cancellationToken);
    }

    /// <summary>2026-11-28-2c06: a design turn's work clone — no run's pipeline, so the project's registries are handed in,
    /// before a package command and once when the clone opens.</summary>
    public async Task BeforeCommandAsync(
        string repoKey, string command, ISandbox sandbox, IReadOnlyList<RegistryConfig> registries, CancellationToken cancellationToken)
    {
        if (Restores(command)) await StageAsync(repoKey, sandbox, registries, cancellationToken);
    }

    /// <summary>2026-11-28-2c06: stages <paramref name="registries"/>' NuGet and npm feeds in a sandbox outside a run.</summary>
    public Task StageAsync(
        string repoKey, ISandbox sandbox, IReadOnlyList<RegistryConfig> registries, CancellationToken cancellationToken) =>
        TolerantAsync(repoKey, () => registryAuth.StageAsync(repoKey, sandbox, registries, cancellationToken), cancellationToken);

    private async Task TolerantAsync(string repoKey, Func<Task> stage, CancellationToken cancellationToken)
    {
        try
        {
            await stage();
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "{Repo}: the feeds could not be staged; the command runs with what was staged", repoKey);
        }
    }
}
