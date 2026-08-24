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
/// runs nothing — a filename is never invented. Everything else falls to the
/// domain profile, and without one is skipped: not every repository in a
/// multi-repo run is buildable (docs, infra, config).
/// <para>
/// Extracted from VerifyPhaseHandler (p0504), which decides what the outcomes MEAN.
/// </para>
/// </summary>
public sealed class VerifyStageResolver(
    DotnetEntryPointDiscovery dotnetDiscovery,
    ProfileCommandPresence presence,
    ILogger<VerifyStageResolver> logger)
{
    public async Task<IReadOnlyList<VerifyStage>> ResolveAsync(
        string key, ProjectMap? map, ISandbox sandbox, string workdir,
        IReadOnlyList<DomainProfileStages> profiles,
        List<string> resolutionFindings, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(profiles);
        ArgumentNullException.ThrowIfNull(resolutionFindings);
        var declared = Declared(key, map);
        if (declared.Count > 0) return declared;

        if (map is null || !IsDotnet(map))
            return await FromProfilesAsync(key, sandbox, profiles, ct);

        return await dotnetDiscovery.DiscoverAsync(key, sandbox, workdir, resolutionFindings, ct);
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

    // p0504: the profile's ordered command list, filtered by the same CanFail rule a
    // declared command passes — a profile author is no more trusted to write a real
    // gate than the analyzer was.
    // p0513: and filtered by what each command SAYS IT NEEDS. One domain word covers
    // repositories of different shapes; a command whose files are absent is skipped,
    // because a red it was never measured for would hide the gates behind it.
    private async Task<List<VerifyStage>> FromProfilesAsync(
        string key, ISandbox sandbox, IReadOnlyList<DomainProfileStages> profiles,
        CancellationToken ct)
    {
        var stages = new List<VerifyStage>();
        foreach (var profile in profiles)
        foreach (var command in profile.Profile.Verify)
        {
            if (!VerificationCommand.CanFail(command.Command))
            {
                logger.LogWarning(
                    "{Key}: domain '{Domain}' declares a {Stage} command '{Command}' that cannot "
                    + "fail — dropping it", key, profile.Profile.Name, command.Stage, command.Command);
                continue;
            }
            if (!await presence.IsSatisfiedAsync(
                    key, profile.Profile.Name, command, sandbox, profile.Workdir, ct))
                continue;
            stages.Add(new VerifyStage(command.Stage, command.Command, profile.Workdir));
        }
        if (stages.Count == 0)
            logger.LogInformation(
                "{Key}: no build/test command declared, no .NET project map and no domain "
                + "profile commands — skipping verification", key);
        return stages;
    }

    private static bool IsDotnet(ProjectMap map) =>
        map.PrimaryLanguage.Trim().ToLowerInvariant() is "csharp" or "fsharp" or "dotnet";

    private static IEnumerable<(string Stage, string? Command)> Stages(CiConfig? ci) =>
        [("build", ci?.BuildCommand), ("test", ci?.TestCommand)];
}
