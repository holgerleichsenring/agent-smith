using AgentSmith.Application.Models;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// p0400/p0504: decides WHICH commands verify one repository, and says so when it
/// can decide nothing. Declared ci commands always win — the analyzer saw the
/// files. A .NET repo declaring neither gets its entry point DISCOVERED from files
/// that actually exist; ambiguous or absent adds a named resolution finding and
/// runs nothing — a filename is never invented. Everything else is skipped: not
/// every repository in a multi-repo run is buildable (docs, infra, config).
/// <para>
/// Extracted from VerifyPhaseHandler (p0504), which decides what the outcomes MEAN.
/// </para>
/// </summary>
public sealed class VerifyStageResolver(
    DotnetEntryPointDiscovery dotnetDiscovery,
    ILogger<VerifyStageResolver> logger)
{
    public async Task<IReadOnlyList<VerifyStage>> ResolveAsync(
        string key, ProjectMap? map, ISandbox sandbox, string workdir,
        List<string> resolutionFindings, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(resolutionFindings);
        var declared = Declared(key, map);
        if (declared.Count > 0) return declared;

        if (map is not null && IsDotnet(map))
            return await dotnetDiscovery.DiscoverAsync(key, sandbox, workdir, resolutionFindings, ct);

        // 2026-08-31-77a8: with the domain profile gone, a non-.NET repository that
        // declared nothing has no third source left. It is skipped rather than
        // guessed at — a repository nothing verifies is reported, never invented.
        logger.LogInformation(
            "{Key}: no build/test command declared and no .NET project map — "
            + "skipping verification", key);
        return [];
    }

    // p0400a: declared ci commands come from the project map, which the analyzer
    // authored against the REPO ROOT (run b9b0: executing them at the context
    // workdir turned a green baseline into MSB1009).
    // p0451: a declared command that cannot fail is not a verification. Run 587c ran
    // `echo Build command placeholder` as a repo's build stage, and the gate reported it
    // green over a repository nothing had compiled.
    private List<VerifyStage> Declared(string key, ProjectMap? map)
    {
        foreach (var (stage, command) in Stages(map?.Ci))
            if (!string.IsNullOrWhiteSpace(command) && !VerificationCommand.CanFail(command))
                logger.LogWarning(
                    "{Key}: the declared {Stage} command '{Command}' cannot fail — ignoring it "
                    + "and resolving an entry point instead", key, stage, command);
        return [.. Stages(map?.Ci)
            .Where(s => VerificationCommand.CanFail(s.Command))
            .Select(s => new VerifyStage(s.Stage, s.Command!, Repository.SandboxWorkPath))];
    }

    private static bool IsDotnet(ProjectMap map) =>
        map.PrimaryLanguage.Trim().ToLowerInvariant() is "csharp" or "fsharp" or "dotnet";

    private static IEnumerable<(string Stage, string? Command)> Stages(CiConfig? ci) =>
        [("build", ci?.BuildCommand), ("test", ci?.TestCommand)];
}
