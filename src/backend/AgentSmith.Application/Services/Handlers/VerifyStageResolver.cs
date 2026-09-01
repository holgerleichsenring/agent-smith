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
/// can decide nothing.
/// <para>
/// 2026-08-31-26d4: what the REPOSITORY DECLARED wins, ahead of everything else. It is
/// the only source authored once and replayed unchanged; the ci pair below it is what
/// the analyzer emitted for this run, which makes that gate differ between runs. Below
/// both, a .NET repo gets its entry point DISCOVERED from files that actually exist;
/// ambiguous or absent adds a named resolution finding and runs nothing — a filename is
/// never invented. Everything else is skipped: not every repository in a multi-repo run
/// is buildable (docs, infra, config).
/// </para>
/// <para>
/// Extracted from VerifyPhaseHandler (p0504), which decides what the outcomes MEAN.
/// </para>
/// </summary>
public sealed class VerifyStageResolver(
    DotnetEntryPointDiscovery dotnetDiscovery,
    DeclaredStagePresence presence,
    ILogger<VerifyStageResolver> logger)
{
    public async Task<IReadOnlyList<VerifyStage>> ResolveAsync(
        string key, ProjectMap? map, ISandbox sandbox, string workdir,
        IReadOnlyList<ContextVerifyStages> contexts,
        VerifyResolutionNotes notes, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(contexts);
        ArgumentNullException.ThrowIfNull(notes);
        if (contexts.Count > 0)
            return await DeclaredAsync(key, sandbox, contexts, notes, ct);

        var inferred = Inferred(key, map);
        if (inferred.Count > 0) return inferred;

        if (map is not null && IsDotnet(map))
            return await dotnetDiscovery.DiscoverAsync(key, sandbox, workdir, notes.Findings, ct);

        // 2026-08-31-77a8: a non-.NET repository that declared nothing has no third
        // source left. It is skipped rather than guessed at — a repository nothing
        // verifies is reported, never invented.
        // 2026-08-28-5f71: the skip is REPORTED as well as logged. Right per repository,
        // it is the whole verdict when it is true of every repository in the run.
        logger.LogInformation(
            "{Key}: no verify block, no build/test command declared and no .NET project "
            + "map — skipping verification", key);
        notes.NothingDeclared(key, map?.PrimaryLanguage);
        return [];
    }

    // 2026-08-31-26d4: the context's own ordered stages, each at ITS OWN workdir — two
    // contexts collapsed into one sandbox each keep their own sub-tree.
    // A declared command that CANNOT FAIL fails resolution rather than being dropped: the
    // declaration is authoritative, so running two of three declared stages and reporting
    // green would be p0451's false green in new clothes.
    // p0513: a stage whose declared path is absent is SKIPPED and reported — a red it was
    // never measured for would hide every real gate behind it.
    private async Task<List<VerifyStage>> DeclaredAsync(
        string key, ISandbox sandbox, IReadOnlyList<ContextVerifyStages> contexts,
        VerifyResolutionNotes notes, CancellationToken ct)
    {
        var stages = new List<VerifyStage>();
        foreach (var context in contexts)
        foreach (var stage in context.Stages)
        {
            if (!VerificationCommand.CanFail(stage.Command))
            {
                notes.Findings.Add(
                    $"{Named(key)}: context '{context.ContextName}' declares a "
                    + $"'{stage.Label}' command '{stage.Command}' that cannot fail, so it "
                    + "proves nothing. A declared stage is authoritative — fix or remove it.");
                return [];
            }
            if (!await presence.IsSatisfiedAsync(key, stage, sandbox, context.Workdir, ct))
                continue;
            stages.Add(new VerifyStage(stage.Label, stage.Command, context.Workdir));
        }
        if (stages.Count == 0)
            notes.EveryDeclaredStageSkipped(key, contexts.Sum(c => c.Stages.Count));
        return stages;
    }

    // p0400a: declared ci commands come from the project map, which the analyzer
    // authored against the REPO ROOT (run b9b0: executing them at the context
    // workdir turned a green baseline into MSB1009).
    // p0451: a command the ANALYZER guessed and that cannot fail is filtered out and the
    // resolver falls through — the framework's own guess is not authoritative.
    private List<VerifyStage> Inferred(string key, ProjectMap? map)
    {
        foreach (var (stage, command) in Stages(map?.Ci))
            if (!string.IsNullOrWhiteSpace(command) && !VerificationCommand.CanFail(command))
                logger.LogWarning(
                    "{Key}: the inferred {Stage} command '{Command}' cannot fail — ignoring it "
                    + "and resolving an entry point instead", key, stage, command);
        return [.. Stages(map?.Ci)
            .Where(s => VerificationCommand.CanFail(s.Command))
            .Select(s => new VerifyStage(s.Stage, s.Command!, Repository.SandboxWorkPath))];
    }

    private static string Named(string key) => string.IsNullOrEmpty(key) ? "(default)" : key;

    private static bool IsDotnet(ProjectMap map) =>
        map.PrimaryLanguage.Trim().ToLowerInvariant() is "csharp" or "fsharp" or "dotnet";

    private static IEnumerable<(string Stage, string? Command)> Stages(CiConfig? ci) =>
        [("build", ci?.BuildCommand), ("test", ci?.TestCommand)];
}
