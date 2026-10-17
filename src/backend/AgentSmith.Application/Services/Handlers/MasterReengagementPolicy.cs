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
    // emitted no parseable verdict ("Your verdict") and a verdict is expected. Model-fitness
    // salvage — the skill instructs "Your verdict"; some models skip the closing artifact.
    // Pure + testable. Mirrors ShouldDriveApply.
    // 2026-09-08-805f: a demand the ledger-complete brake already appended INSIDE the pass is
    // this re-drive; a second one would be the same prompt against the same silence.
    internal static bool ShouldNudgeForVerdict(
        string? pipelineName, MasterVerification? verification, bool verdictDemandedInPass) =>
        verification is null
        && !verdictDemandedInPass
        && !string.IsNullOrEmpty(pipelineName)
        && PipelinePresets.ExpectsCodeChanges(pipelineName);

    // p0341c/p0341e: the re-engagement predicate. Re-engages while the run is OBJECTIVELY incomplete —
    // a red verdict with actionable steps left, done steps the diff does not back, or a ratified contract
    // not yet satisfied — never merely while the model's checklist reads pending. Honest RED over a
    // drained ledger is respected; budget exhaustion always stops; a silent master gets one re-drive.
    // p0406: reengagePass and verdictDemandedInPass are REQUIRED, never defaulted.
    // 2026-10-13-9a59f: the branches and the reason each gives live in ReengageReasonPolicy.
    // 2026-10-17-a196c: the claims-free reading; the loop alone resolves met claims (AcceptanceClaimObjection).
    internal static bool ShouldReengage(
        string? pipelineName, ProgressLedger ledger, MasterVerification? verification,
        bool budgetExhausted, IReadOnlyList<string> ratifiedCriteria, IReadOnlyList<CodeChange> changes,
        int reengagePass, bool verdictDemandedInPass) =>
        ReengageReasonPolicy.Decide(pipelineName, ledger, verification, budgetExhausted, ratifiedCriteria,
            changes, reengagePass, verdictDemandedInPass, unresolvedClaims: []).Reengage;

    // p0341e: the ratified acceptance criteria for this run (empty when nothing was negotiated —
    // fix-bug self-planning, ticketless runs). Same source the keystone reads.
    // p0393a: the current SPEC's done-list, falling back to a ratified expectation for
    // pipelines that still negotiate one. Same source the keystone reads.
    internal static IReadOnlyList<string> RatifiedCriteria(PipelineContext pipeline) =>
        Specs.AcceptanceCriteria.For(pipeline);
}
