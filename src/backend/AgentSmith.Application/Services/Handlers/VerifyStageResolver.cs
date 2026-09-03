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
/// both, nothing is guessed: not every repository in a multi-repo run is buildable
/// (docs, infra, config), and a repository nothing verifies is reported rather than
/// invented. 2026-09-03-ee12: and nothing branches on a language — what builds a
/// repository is the analyzer's answer for every stack, never a per-language search the
/// framework would have to grow a row at a time.
/// </para>
/// <para>
/// 2026-09-03-7bac: every stage runs at the REPOSITORY ROOT, and no declared path places
/// one. meta.workdir says where a context's SOURCE lives, not where it is built or where
/// its tests are — reading it as a command's directory anticipates that tests sit beside
/// the component, true in some repositories of every language and false in others. The
/// root is not a better guess: it is the frame the commands were WRITTEN in, since the
/// analyzer's tools, the master's run_command and the sandbox default all resolve '.' to
/// /work. A command needing another directory carries its own cd.
/// </para>
/// <para>
/// Extracted from VerifyPhaseHandler (p0504), which decides what the outcomes MEAN.
/// </para>
/// </summary>
public sealed class VerifyStageResolver(
    DeclaredStagePresence presence,
    ILogger<VerifyStageResolver> logger)
{
    public async Task<IReadOnlyList<VerifyStage>> ResolveAsync(
        string key, ProjectMap? map, ISandbox sandbox,
        IReadOnlyList<ContextVerifyStages> contexts,
        VerifyResolutionNotes notes, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(contexts);
        ArgumentNullException.ThrowIfNull(notes);
        if (contexts.Count > 0)
            return await DeclaredAsync(key, sandbox, contexts, notes, ct);

        var inferred = Inferred(key, map);
        if (inferred.Count > 0) return inferred;

        // 2026-08-31-77a8: a repository that declared nothing has no source left. It is
        // skipped rather than guessed at — a repository nothing verifies is reported,
        // never invented.
        // 2026-08-28-5f71: the skip is REPORTED as well as logged. Right per repository,
        // it is the whole verdict when it is true of every repository in the run.
        logger.LogInformation(
            "{Key}: no verify block and no build/test command declared — skipping "
            + "verification", key);
        notes.NothingDeclared(key, map?.PrimaryLanguage);
        return [];
    }

    // 2026-08-31-26d4: the context's own ordered stages — two contexts collapsed into one
    // sandbox contribute both declarations. A declared command that CANNOT FAIL fails
    // resolution rather than being dropped: the declaration is authoritative, so running
    // two of three declared stages and reporting green would be p0451's false green.
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
            if (!await presence.IsSatisfiedAsync(key, stage, sandbox, ct))
                continue;
            stages.Add(new VerifyStage(stage.Label, stage.Command, Repository.SandboxWorkPath));
        }
        if (stages.Count == 0)
            notes.EveryDeclaredStageSkipped(key, contexts.Sum(c => c.Stages.Count));
        return stages;
    }

    // 2026-09-03-7bac: the analyzer's ci commands run where the analyzer stood — its
    // tools resolve '.' to /work whatever sub-tree it was pointed at, so its paths are
    // root-relative. Run 5a18 proves it both ways: one directory down, its bare
    // 'dotnet build' passed while 'dotnet test <sibling test project>' could not find a
    // project that exists at the root.
    // p0451: a command the ANALYZER guessed and that cannot fail is filtered out and the
    // resolver falls through — the framework's own guess is not authoritative.
    private List<VerifyStage> Inferred(string key, ProjectMap? map)
    {
        foreach (var (stage, command) in Stages(map?.Ci))
            if (!string.IsNullOrWhiteSpace(command) && !VerificationCommand.CanFail(command))
                logger.LogWarning(
                    "{Key}: the inferred {Stage} command '{Command}' cannot fail — ignoring it",
                    key, stage, command);
        return [.. Stages(map?.Ci)
            .Where(s => VerificationCommand.CanFail(s.Command))
            .Select(s => new VerifyStage(s.Stage, s.Command!, Repository.SandboxWorkPath))];
    }

    private static string Named(string key) => string.IsNullOrEmpty(key) ? "(default)" : key;

    private static IEnumerable<(string Stage, string? Command)> Stages(CiConfig? ci) =>
        [("build", ci?.BuildCommand), ("test", ci?.TestCommand)];
}
