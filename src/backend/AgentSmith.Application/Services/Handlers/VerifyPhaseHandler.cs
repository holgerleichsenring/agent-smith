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
/// 2026-08-31-26d4: a repository that DECLARES its stages in context.yaml is verified by
/// exactly those, ahead of every other source — the one gate authored once instead of
/// re-derived per run. AnalyzeCode populates <see cref="CiConfig.BuildCommand"/> and
/// <see cref="CiConfig.TestCommand"/> per repo; those inferred commands come next.
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
/// p0430: what a phase SHIPS is no longer declared. p0400 introduced ships_code so a
/// knowledge phase without a diff would not fail the no-diff rule, and p0421 deleted
/// that rule — whether a build has anything to prove is read from the branch, per repo,
/// which is the same answer without a field to maintain.
/// </summary>
public sealed class VerifyPhaseHandler(
    VerifyStageResolver stageResolver,
    ContextVerifyStagesResolver declaredStages,
    SandboxTargets sandboxTargets,
    VerifyCommandRunner commandRunner,
    DeliveryDiff deliveryDiff,
    PhaseAccounting accounting,
    IPhaseProgressRecorder progress,
    ILogger<VerifyPhaseHandler> logger)
    : ICommandHandler<VerifyPhaseContext>
{
    private const int ReasonTailChars = 800;

    public async Task<CommandResult> ExecuteAsync(
        VerifyPhaseContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!sandboxTargets.TryResolve(context.Pipeline, out var sandboxes, out var discoveries))
            return await RecordAsync(
                context, CommandResult.Ok("No sandboxes in pipeline context; nothing to verify."),
                cancellationToken);

        // p0422: whether the mechanical gates have anything to say is read from the
        // BRANCH, not from the working tree. Run 18 skipped both builds as "untouched"
        // over a branch that carried the whole delivery — the master had committed as it
        // went, so the tree was clean and the criterion "the build exits 0" then had no
        // build to point at. Delivery is a property of the branch; so is the question of
        // whether a build has anything to prove.
        var delivered = new Dictionary<string, string>();
        foreach (var (key, sandbox) in sandboxes)
        {
            var diff = await deliveryDiff.ForBranchAsync(sandbox, cancellationToken);
            delivered[key] = diff.Failed ? string.Empty : diff.Text;
        }
        var dirty = delivered.ToDictionary(e => e.Key, e => DeliveryDiff.CarriesSource(e.Value));
        var touchedSource = dirty.Values.Any(d => d);

        var outcomes = new List<VerifyOutcome>();
        var resolutionFindings = new List<string>();
        foreach (var (key, sandbox) in sandboxes)
        {
            // A repo whose BRANCH carries no source change has nothing for a build to
            // prove — running it would gate the phase on pre-existing state.
            if (!dirty.GetValueOrDefault(key))
            {
                logger.LogInformation(
                    "{Key}: this branch carries no source change — skipping build/test", key);
                continue;
            }

            var map = context.RepoProjectMaps.TryGetValue(key, out var m) ? m : null;
            var workdir = SandboxWorkdir.Resolve(
                discoveries.TryGetValue(key, out var discovery) ? discovery.Workdir : null);

            // 2026-08-31-26d4: the PER-SANDBOX CONTEXT LIST, not the representative
            // discovery above — two contexts resolving one image collapse into one
            // sandbox, and each declaration runs at its own workdir.
            foreach (var stage in await stageResolver.ResolveAsync(
                key, map, sandbox, workdir, declaredStages.For(context.Pipeline, key),
                resolutionFindings, cancellationToken))
            {
                var outcome = await commandRunner.RunAsync(
                    key, stage.Stage, sandbox, stage.Cwd, stage.Command, cancellationToken);
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
        if (!mechanical.IsSuccess)
        {
            // The phase's account IS the mechanical failure. Without this the run reports
            // "nobody accounted for anything" over a build that failed loudly one step
            // earlier — true, useless, and pointing away from the cause.
            RunAccountLedger.RecordProblem(context.Pipeline, sandboxes.Keys, mechanical.Message);
            return await RecordAsync(context, mechanical, cancellationToken);
        }

        var ranCommands = Specs.PhaseEvidence.From(outcomes, context.Pipeline);
        var accounts = await accounting.TakeAsync(
            context.Pipeline, sandboxes, ranCommands, cancellationToken);
        context.Pipeline.Set(ContextKeys.PhaseAccounts, accounts);
        RunAccountLedger.Record(context.Pipeline, accounts);
        var verdict = PhaseVerdict.From(mechanical, accounts);
        return await RecordAsync(
            context, verdict.IsSuccess ? verdict : Repairable(context, accounts, verdict),
            cancellationToken);
    }

    /// <summary>
    /// p0438: an outstanding criterion goes back to the agent that can close it, ONCE,
    /// before it becomes the operator's problem.
    /// <para>
    /// The accountant produces the most actionable artefact of a run — what is missing, in
    /// the contract's own words, checked against the real branch. Until now that was
    /// rendered into an error message for the operator while the agent that wrote the work,
    /// and is the only thing that could finish it, never saw it. The operator's question
    /// named the defect: a correct "no" is half a mechanism.
    /// </para>
    /// <para>
    /// One pass, not a loop: a second correct no is information, a third is a carousel. The
    /// repair splices [master, commit, verify] — the same shape p0437 fixed — so it inherits
    /// that ordering guarantee instead of restating it.
    /// </para>
    /// </summary>
    private CommandResult Repairable(
        VerifyPhaseContext context, IReadOnlyList<SpecAccount> accounts, CommandResult verdict)
    {
        if (!PhaseVerdict.IsRepairable(accounts)) return verdict;
        if (context.Pipeline.TryGet<bool>(ContextKeys.PhaseRepairAttempted, out var tried) && tried)
            return verdict;

        var outstanding = PhaseVerdict.Outstanding(accounts);
        context.Pipeline.Set(ContextKeys.PhaseRepairAttempted, true);
        context.Pipeline.Set(ContextKeys.OutstandingCriteria, outstanding.ToList());
        logger.LogInformation(
            "{Count} criterion(s) outstanding — handing the list back to the agent for one "
            + "repair pass before this becomes a verdict.", outstanding.Count);

        // p0341g: stamped with the phase they repair — a repeated step belongs to the same
        // phase as the step it repeats, in the rail and in every per-phase rollup.
        var phaseId = context.Pipeline.TryGet<PhaseDraft>(ContextKeys.PhaseSpec, out var d)
            ? d?.PhaseId : null;
        return CommandResult.OkAndContinueWith(
            $"{outstanding.Count} criterion(s) outstanding; one repair pass follows",
            [.. PhaseVerdict.RepairSteps(phaseId)]);
    }

    // p0393a: verification is what makes a phase DONE, so this is where the sequence's
    // per-phase table is written. A stopped sequence leaves a half-migrated repository,
    // and the pull request states which phases are through only because this ran.
    // p0466: one writer — IPhaseProgressRecorder puts the standing in the per-phase
    // table AND on the event stream, so the phase reaches the server as a row.
    private async Task<CommandResult> RecordAsync(
        VerifyPhaseContext context, CommandResult result, CancellationToken ct)
    {
        if (!context.Pipeline.TryGet<PhaseDraft>(ContextKeys.PhaseSpec, out var draft) || draft is null)
            return result;
        if (result.IsSuccess)
            await progress.RecordAsync(
                context.Pipeline, draft.PhaseId, PhaseRunState.Done, cancellationToken: ct);
        else
            await progress.RecordAsync(
                context.Pipeline, draft.PhaseId, PhaseRunState.Failed, FailingCommandOf(result),
                cancellationToken: ct);
        return result;
    }

    private static string FailingCommandOf(CommandResult result) =>
        result.Message.Split('\n', 2)[0].Trim();

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
            return CommandResult.Ok($"Verified: {Describe(ran)} green");

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

    private static string Tail(string? text, int max) =>
        string.IsNullOrEmpty(text) ? string.Empty
        : text.Length <= max ? text
        : text[^max..];
}
