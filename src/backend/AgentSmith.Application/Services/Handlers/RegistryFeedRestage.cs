using System.Text.RegularExpressions;
using AgentSmith.Contracts.Commands;
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
    public async Task StageAsync(PipelineContext pipeline, string repoKey, ISandbox sandbox, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        try
        {
            await registryAuth.StageAsync(pipeline, repoKey, sandbox, cancellationToken, generic: false);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "{Repo}: the feeds could not be staged again; the command runs with what was staged", repoKey);
        }
    }
}
