using CommandLineStringSplitter = System.CommandLine.Parsing.CommandLineStringSplitter;
using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Sandbox.Wire;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// p0393: runs the repository's own build and test commands after the master and
/// fails the run before any PR when either is red.
///
/// p0216 moved build+test verification to the coding master as a RESPONSIBILITY —
/// it runs the repo's tests itself through real run_command calls — and removed the
/// rigid Test step. What it left behind was the absence of a second opinion: nothing
/// refused the pull request when the build was red, so "green" was a claim by the
/// same party that produced the code.
///
/// AnalyzeCode populates <see cref="CiConfig.BuildCommand"/> and
/// <see cref="CiConfig.TestCommand"/> per repo; those declared commands always win.
/// p0400: when a .NET repo declares NEITHER, the entry point is DISCOVERED (a single
/// *.sln up to depth 2, else a single *.csproj at the context workdir) — never
/// guessed. An ambiguous or absent entry point is a named RESOLUTION failure that
/// says what was searched and where; it is not reported as a compile result, because
/// no command was executed. A non-.NET repo declaring neither command is SKIPPED,
/// not failed: not every repository in a multi-repo run is buildable (docs, infra,
/// config). A run in which NO repo ran a command reports that plainly rather than
/// passing quietly — an unverifiable run must not be indistinguishable from a
/// verified one.
///
/// p0400: a phase whose ratified spec declares <c>ships_code: false</c> ships
/// knowledge, by design without a source diff. When such a phase produced no diff
/// the build gate is skipped entirely (nothing it could measure changed); when it
/// did touch the tree, a red command is compared against the pre-phase baseline —
/// the phase must not make the build WORSE, but a repo that was already red does
/// not fail a knowledge phase.
/// </summary>
public sealed class VerifyPhaseHandler(
    SandboxGitOperations gitOps,
    ISandboxFileReaderFactory readerFactory,
    SandboxTargets sandboxTargets,
    VerifyCommandRunner commandRunner,
    PhaseAccounting accounting,
    ILogger<VerifyPhaseHandler> logger)
    : ICommandHandler<VerifyPhaseContext>
{
    private const int ReasonTailChars = 800;

    private const int EntryPointSearchDepth = 2;

    public async Task<CommandResult> ExecuteAsync(
        VerifyPhaseContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!sandboxTargets.TryResolve(context.Pipeline, out var sandboxes, out var discoveries))
            return Record(context, CommandResult.Ok("No sandboxes in pipeline context; nothing to verify."));

        // p0421: whether the mechanical gates have anything to say is READ from the tree,
        // not declared. A phase that touched no source has nothing for a build to be green
        // about — and the declaration that used to say so (ships_code) existed only to
        // except the old gate from its own question.
        var dirty = new Dictionary<string, bool>();
        foreach (var (key, sandbox) in sandboxes)
            dirty[key] = await gitOps.HasWorkingChangesAsync(sandbox, cancellationToken);
        var checkpointed = CheckpointedRepos(context.Pipeline);
        var touchedSource = dirty.Values.Any(d => d) || checkpointed.Values.Any(c => c);

        var outcomes = new List<VerifyOutcome>();
        var resolutionFindings = new List<string>();
        foreach (var (key, sandbox) in sandboxes)
        {
            // An untouched repo cannot differ from its own baseline — running its build
            // would gate the phase on pre-existing state. "Untouched" has to include the
            // work the master already CHECKPOINTED, or a phase that committed as it went
            // would skip the very build that proves it: a clean tree is not an empty one.
            if (!dirty.GetValueOrDefault(key) && !checkpointed.GetValueOrDefault(key))
            {
                logger.LogInformation("{Key}: this repo's tree is untouched — skipping build/test", key);
                continue;
            }

            var map = context.RepoProjectMaps.TryGetValue(key, out var m) ? m : null;
            var workdir = SubTreeWorkdir(NormalizeWorkdir(
                discoveries.TryGetValue(key, out var discovery) ? discovery.Workdir : null));

            foreach (var (stage, command, cwd) in await ResolveStagesAsync(
                key, map, sandbox, workdir, resolutionFindings, cancellationToken))
            {
                var outcome = await commandRunner.RunAsync(key, stage, sandbox, cwd, command, cancellationToken);
                if (outcome.ExitCode != 0 && !touchedSource)
                    outcome = await CompareAgainstBaselineAsync(outcome, sandbox, cwd, cancellationToken);
                outcomes.Add(outcome);
                // A red build makes the test result meaningless; stop this repo here so the
                // failure reason names the build rather than a downstream cascade.
                if (outcome.ExitCode != 0) break;
            }
        }

        // p0420: the mechanical gates answer HARM; the account answers DELIVERY. A red
        // build wins first — an account taken over a tree that does not compile would be
        // an opinion about work nobody can ship.
        var mechanical = BuildAggregateResult(outcomes, resolutionFindings, touchedSource);
        if (!mechanical.IsSuccess) return Record(context, mechanical);

        var accounts = await accounting.TakeAsync(context.Pipeline, sandboxes, cancellationToken);
        context.Pipeline.Set(ContextKeys.PhaseAccounts, accounts);
        RunAccountLedger.Record(context.Pipeline, accounts);
        return Record(context, PhaseVerdict.From(mechanical, accounts));
    }

    // p0393a: verification is what makes a phase DONE, so this is where the sequence's
    // per-phase table is written. A stopped sequence leaves a half-migrated repository,
    // and the pull request states which phases are through only because this ran.
    private static CommandResult Record(VerifyPhaseContext context, CommandResult result)
    {
        if (!context.Pipeline.TryGet<SpecSequenceProgress>(
                ContextKeys.SpecSequenceProgress, out var progress) || progress is null)
            return result;
        if (!context.Pipeline.TryGet<PhaseDraft>(ContextKeys.PhaseSpec, out var draft) || draft is null)
            return result;
        context.Pipeline.Set(
            ContextKeys.SpecSequenceProgress,
            result.IsSuccess
                ? progress.With(draft.PhaseId, PhaseRunState.Done)
                : progress.With(draft.PhaseId, PhaseRunState.Failed, FailingCommandOf(result)));
        return result;
    }

    private static string FailingCommandOf(CommandResult result) =>
        result.Message.Split('\n', 2)[0].Trim();

    private static Dictionary<string, bool> CheckpointedRepos(PipelineContext pipeline) =>
        pipeline.TryGet<Dictionary<string, bool>>(ContextKeys.CheckpointedRepos, out var map)
        && map is not null ? map : [];

    /// <summary>
    /// p0400: command resolution. Declared context commands always win. A .NET repo
    /// declaring neither gets its entry point DISCOVERED from files that actually
    /// exist; ambiguous or absent adds a named resolution finding and runs nothing —
    /// a filename is never invented. Anything else keeps the p0393 skip.
    /// </summary>
    private async Task<IReadOnlyList<(string Stage, string Command, string Cwd)>> ResolveStagesAsync(
        string key, ProjectMap? map, ISandbox sandbox, string workdir,
        List<string> resolutionFindings, CancellationToken ct)
    {
        // p0400a: declared ci commands come from the project map, which the analyzer
        // authored against the REPO ROOT (run b9b0: executing them at the context
        // workdir turned a green baseline into MSB1009). Discovered entry points keep
        // the workdir — their paths are built relative to where they were found.
        var declared = Stages(map?.Ci)
            .Where(s => !string.IsNullOrWhiteSpace(s.Command))
            .Select(s => (s.Stage, s.Command!, Repository.SandboxWorkPath))
            .ToList();
        if (declared.Count > 0) return declared;

        if (map is null || !IsDotnet(map))
        {
            logger.LogInformation(
                "{Key}: no build/test command declared and no .NET project map — skipping verification", key);
            return [];
        }

        var entries = await readerFactory.Create(sandbox).ListAsync(workdir, EntryPointSearchDepth, ct);
        var relative = entries
            .Select(e => Relative(e, workdir))
            .Where(p => p.Length > 0)
            .ToList();
        var solutions = relative
            .Where(p => p.EndsWith(".sln", StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (solutions.Count == 1)
        {
            logger.LogInformation("{Key}: discovered build entry point {Solution}", key, solutions[0]);
            return [("build", $"dotnet build \"{solutions[0]}\"", workdir)];
        }
        var projects = relative
            .Where(p => !p.Contains('/') && p.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (solutions.Count == 0 && projects.Count == 1)
        {
            logger.LogInformation("{Key}: discovered build entry point {Project}", key, projects[0]);
            return [("build", $"dotnet build \"{projects[0]}\"", workdir)];
        }

        resolutionFindings.Add(
            $"{Where(key)}: searched for a single *.sln up to depth {EntryPointSearchDepth} under "
            + $"{workdir} and a single *.csproj at {workdir} — found {solutions.Count} solution(s) "
            + $"and {projects.Count} top-level project(s). No command was executed; declare "
            + "ci.build_command / ci.test_command to make this repo verifiable.");
        return [];
    }

    private static bool IsDotnet(ProjectMap map) =>
        map.PrimaryLanguage.Trim().ToLowerInvariant() is "csharp" or "fsharp" or "dotnet";

    // Entries may come back absolute or relative to the listed root; normalize to
    // workdir-relative so the command runs against the path it was discovered at.
    private static string Relative(string entry, string workdir)
    {
        var normalized = entry.Replace('\\', '/');
        if (normalized.StartsWith(workdir + "/", StringComparison.Ordinal))
            normalized = normalized[(workdir.Length + 1)..];
        return normalized.TrimStart('/').Trim();
    }

    /// <summary>
    /// p0400: a ships_code:false phase must not make the build WORSE than before it —
    /// but a repo already red at the pre-phase baseline does not fail a knowledge
    /// phase. The phase's working changes are stashed, the same command re-run, and
    /// the changes restored. No baseline (nothing to stash) keeps the honest red.
    /// </summary>
    private async Task<VerifyOutcome> CompareAgainstBaselineAsync(
        VerifyOutcome red, ISandbox sandbox, string workdir, CancellationToken ct)
    {
        if (!await gitOps.StashWorkingChangesAsync(sandbox, ct))
        {
            logger.LogWarning(
                "{Key}: no pre-phase baseline could be established (nothing to stash) — "
                + "keeping the red {Stage} result", red.Key, red.Stage);
            return red;
        }
        try
        {
            var baseline = await commandRunner.RunAsync(
                red.Key, red.Stage, sandbox, workdir, red.Command, ct);
            if (baseline.ExitCode == 0) return red;
            logger.LogInformation(
                "{Key}: {Stage} '{Command}' is red at the pre-phase baseline too (exit {Exit}) — "
                + "this phase made nothing worse", red.Key, red.Stage, red.Command, baseline.ExitCode);
            return red with { ExitCode = 0, NotWorseThanBaseline = true };
        }
        finally
        {
            await gitOps.RestoreStashedChangesAsync(sandbox, ct);
        }
    }

    private static IEnumerable<(string Stage, string? Command)> Stages(CiConfig? ci) =>
        [("build", ci?.BuildCommand), ("test", ci?.TestCommand)];

    private static CommandResult BuildAggregateResult(
        IReadOnlyList<VerifyOutcome> outcomes, IReadOnlyList<string> resolutionFindings, bool touchedSource)
    {
        // A resolution failure is its own verdict: no command ran for that repo, so
        // there is no compile result to report — and an unresolvable repo must not
        // pass silently either.
        if (resolutionFindings.Count > 0)
            return CommandResult.Fail(
                "Verification could not resolve a build entry point — no command was executed "
                + "(this is a resolution failure, not a build result). "
                + string.Join(" ", resolutionFindings));

        var ran = outcomes.Where(o => !o.Skipped).ToList();
        if (ran.Count == 0)
            return touchedSource
                ? CommandResult.Ok(
                    "Nothing to verify: no repository declared a build or test command. "
                    + "This run is UNVERIFIED — add ci.build_command / ci.test_command to make the gate real.")
                : CommandResult.Ok(
                    "No repository had working-tree changes to verify; the phase is judged "
                    + "by what its criteria account for.");

        var failed = ran.Where(o => o.ExitCode != 0).ToList();
        if (failed.Count == 0)
        {
            var notWorse = ran.Where(o => o.NotWorseThanBaseline).ToList();
            return CommandResult.Ok(
                $"Verified: {Describe(ran)} green"
                + (notWorse.Count == 0
                    ? string.Empty
                    : $" ({Describe(notWorse)} red at the pre-phase baseline too — not made worse by this phase)"));
        }

        var first = failed[0];
        var detail = Tail(first.Output, ReasonTailChars);
        var reason = string.IsNullOrWhiteSpace(detail) ? string.Empty : $"\n{detail}";
        return CommandResult.Fail(
            $"Verification failed: {Where(first.Key)} {first.Stage} '{first.Command}' exited {first.ExitCode}."
            + $" No pull request is opened for a red build.{reason}");
    }

    private static string Where(string key) =>
        string.IsNullOrEmpty(key) ? "(default)" : key;

    private static string Describe(IReadOnlyList<VerifyOutcome> ran) =>
        string.Join(", ", ran
            .GroupBy(o => Where(o.Key))
            .Select(g => $"{g.Key} [{string.Join('+', g.Select(o => o.Stage))}]"));

    private static string SubTreeWorkdir(string workdir) =>
        workdir == "." ? Repository.SandboxWorkPath : $"{Repository.SandboxWorkPath}/{workdir}";

    private static string NormalizeWorkdir(string? workdir)
    {
        if (string.IsNullOrWhiteSpace(workdir)) return ".";
        var trimmed = workdir.Trim().Replace('\\', '/').Trim('/');
        return trimmed.Length == 0 ? "." : trimmed;
    }

    private static string Combine(string? stdout, string? stderr) =>
        string.Join('\n', new[] { stdout, stderr }.Where(s => !string.IsNullOrWhiteSpace(s)));

    private static string Tail(string? text, int max) =>
        string.IsNullOrEmpty(text) ? string.Empty
        : text.Length <= max ? text
        : text[^max..];
}
