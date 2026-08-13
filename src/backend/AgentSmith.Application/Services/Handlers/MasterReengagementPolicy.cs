using AgentSmith.Application.Extensions;
using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Loop;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Progress;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Decides whether a master that fell silent gets re-engaged, and whether its
/// acceptance claim stands on its own. Pure predicates over the loop result,
/// verification and ledger; p0403 lifted them out of the handler.
/// </summary>
internal static class MasterReengagementPolicy
{
    // p0255: re-prompt the master to APPLY when the run expects edited source
    // (fix-bug / add-feature; not mad-discussion / scans) but it wrote only
    // run-record artifacts — a plan with zero source edits. Pure + testable.
    internal static bool ShouldDriveApply(string? pipelineName, IReadOnlyList<CodeChange> changes) =>
        !string.IsNullOrEmpty(pipelineName)
        && PipelinePresets.ExpectsCodeChanges(pipelineName)
        && !changes.Any(c => !RunRecordPaths.IsRunRecordPath(c.Path.ToString()));

    // p0263: re-prompt the master to EMIT ITS VERDICT when it changed source but
    // emitted no parseable Phase 4 verdict and a verdict is expected (a green-tests
    // pipeline). Model-fitness salvage — the skill instructs Phase 4; some models skip
    // the closing artifact. Pure + testable. Mirrors ShouldDriveApply.
    internal static bool ShouldNudgeForVerdict(string? pipelineName, MasterVerification? verification) =>
        verification is null
        && !string.IsNullOrEmpty(pipelineName)
        && PipelinePresets.ExpectsGreenTests(pipelineName);

    // p0341c/p0341e: the re-engagement predicate — pure + testable, mirroring ShouldDriveApply /
    // ShouldNudgeForVerdict. Re-engages the open loop while the run is OBJECTIVELY incomplete —
    // not merely while the MODEL still reports pending steps. A model that drains the ledger by
    // marking steps done WITHOUT doing them (or a plan that under-seeds the repo) previously
    // defeated re-engagement: HasActionablePending went false and the loop quit early, leaving
    // the keystone to catch the lie only at the very end. Now three signals, first two OBJECTIVE:
    //   (1) the model's own checklist still has actionable steps (the original signal), OR
    //   (2) a DONE-marked step's declared target is absent from the actual diff (marking-without-
    //       doing — the diff is unfakeable), OR
    //   (3) the ratified acceptance contract is not yet objectively satisfied (build/tests green
    //       AND every criterion met/justified) — a drained ledger over an unmet contract is not
    //       a real completion.
    // Honest RED is respected only when the ledger is drained (p0363) — RED with open
    // actionable steps is a mid-work status report and gets re-driven. Budget exhaustion
    // always stops. Bounded by the caller's forward-progress gate + the hard safety cap —
    // a red re-drive that moves nothing ends the loop after one pass.
    internal static bool ShouldReengage(
        string? pipelineName, ProgressLedger ledger, MasterVerification? verification,
        bool budgetExhausted, IReadOnlyList<string> ratifiedCriteria, IReadOnlyList<CodeChange> changes)
    {
        if (string.IsNullOrEmpty(pipelineName) || !PipelinePresets.ExpectsCodeChanges(pipelineName))
            return false;
        if (budgetExhausted) return false;
        // p0363: honest RED is terminal ONLY when the model has nothing actionable left.
        // A RED verdict WITH open checklist items ("Build solutions and fix compile
        // issues" marked NOW) is a status report mid-work, not a verdict of
        // impossibility — the observed failure mode: the model runs its verification,
        // sees the red build, emits FAILED and stops with $43 of budget and 80 minutes
        // of wall-time left. Re-drive it; the caller's forward-progress gate still ends
        // the loop after one red pass that moves nothing, so persistence stays bounded
        // and justified surrender (RED + drained ledger) is still respected.
        // p0363 + p0393: on a RED verdict the ledger is the only thing that separates
        // "gave up early" from "genuinely stuck". Red with open actionable items is a status
        // report mid-work — the observed failure was a model emitting FAILED with $43 of
        // budget left while its own checklist said "fix the build". Red with a drained ledger
        // is justified surrender and is respected. That is the ledger QUALIFYING a driver
        // that already exists, not originating one; the caller's forward-progress gate still
        // ends the loop after a red pass that moves nothing.
        if (verification?.Status == VerificationStatus.Failed)
            return ledger.HasActionablePending;

        // p0393: the ledger no longer VOTES on whether to continue. It keeps its other two
        // jobs — memory, and progress a watcher can read — but a checklist the model writes
        // and then reads back as a reason to keep working is a loop carrying its own engine.
        // The distinction an interactive agent already makes: a reminder to RECORD progress
        // is not a reason to CONTINUE. Claude Code nudges "you have not used the todo tool
        // recently" and ends the sentence with "ignore if not applicable"; the turn still ends
        // when the request is answered, never when the list is empty. What re-drives the master
        // here is unfinished WORK — done steps the diff does not back, and unmet ratified
        // criteria — not an unticked box.
        if (ProgressLedgerCoverage.UnbackedDoneSteps(ledger, changes).Count > 0) return true;
        if (ratifiedCriteria.Count > 0
            && !AcceptanceObjectivelySatisfied(verification, ratifiedCriteria.Count))
            return true;
        return false;
    }

    // p0341e: the objective acceptance gate mirrored from RunOutcomeKeystone.EvaluateAcceptance
    // (the single definition of done). The contract is satisfied ONLY when the build/tests are
    // green (or genuinely test-less) AND every ratified criterion has a reported disposition that
    // is Met or justified not-applicable. A missing verdict, a non-green status, or any unmet /
    // missing disposition => not satisfied. Pure + testable.
    internal static bool AcceptanceObjectivelySatisfied(MasterVerification? verification, int criteriaCount)
    {
        if (criteriaCount == 0) return true;
        if (verification is null) return false;
        if (verification.Status is not (VerificationStatus.Green or VerificationStatus.NoTests))
            return false;
        var dispositions = verification.AcceptanceDispositions;
        if (dispositions is null || dispositions.Count < criteriaCount) return false;
        for (var i = 0; i < criteriaCount; i++)
        {
            var d = dispositions[i];
            if (d.Status == AcceptanceStatus.Met) continue;
            if (d.Status == AcceptanceStatus.NotApplicable && !string.IsNullOrWhiteSpace(d.Evidence)) continue;
            return false;
        }
        return true;
    }

    // p0341e: the ratified acceptance criteria for this run (empty when nothing was negotiated —
    // fix-bug self-planning, ticketless runs). Same source the keystone reads.
    // p0393a: the current PHASE's done-list, falling back to a ratified expectation for
    // pipelines that still negotiate one. Same source the keystone reads.
    internal static IReadOnlyList<string> RatifiedCriteria(PipelineContext pipeline) =>
        Specs.AcceptanceCriteria.For(pipeline);
}
