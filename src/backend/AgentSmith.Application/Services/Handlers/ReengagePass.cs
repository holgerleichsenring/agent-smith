using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Passes;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// 2026-10-13-9a59f: ONE re-engagement pass of the open loop — the warm nudge (current ledger plus the
/// working-state block), the drive, the reparse, and the forward-progress verdict over it. A pass that
/// throws is recovery, not idleness: it keeps the prior state and lets the loop decide again.
/// </summary>
public sealed class ReengagePass(
    Loop.IAgenticLoopRunner loopRunner, SandboxWorkingTreeReader workingTree, ReengageStopLog stopLog,
    ILogger<ReengagePass> logger)
{
    internal async Task<ReengagePassResult> RunAsync(
        ReengageSession s, ReengageState state, IReadOnlyList<string> criteria, CancellationToken ct)
    {
        var skill = s.Context.MasterSkillName;
        var (loopResult, changes, verification) = state;
        MasterVerification? emitted = null;
        var passThrew = false;
        MasterBlockedClaim? blockedClaim = null;
        var toolCallsInPass = 0;
        // p0391: the ids the checklist carried INTO this pass — a pass that ends with only ids it invented
        // itself, having written nothing, refilled its own work.
        var ledgerIdsAtPassStart = s.Progress.GetLedger().Entries.Select(e => e.Id).ToHashSet(StringComparer.Ordinal);
        var changesAtPassStart = changes;

        logger.LogInformation(
            "Master '{Skill}' re-engaging the open loop — {Remaining} actionable step(s) remain, budget OK; acceptance: {Acceptance}",
            skill, s.Progress.GetLedger().ActionablePending.Count,
            MasterAcceptanceGate.Judge(verification, criteria, changes.Count > 0).Describe());
        try
        {
            // p0411: the working tree, read once per pass, so the nudge opens with the changed paths.
            var changedPaths = await ReadChangedPathsAsync(s.Context.Pipeline, ct);
            var nudge = MasterNudges.BuildReengageNudge(
                s.UserPrompt, s.Progress.GetLedger(), s.Log.GetDecisions(), verification,
                changedPaths, MasterPipelineFacts.StagedRegistries(s.Context.Pipeline));
            var reengaged = await loopRunner.RunAsync(
                s.Request with { UserPrompt = nudge, PriorMessages = s.Conversation.Thread() }, ct);
            s.Conversation.Continued(nudge, reengaged.Response);
            // p0341e: a no-op for the coding master — its governor hook already recorded the spend.
            s.TrackMasterResponse(reengaged.Response);
            loopResult = reengaged;
            changes = s.Fs.GetChanges();
            emitted = MasterVerificationParser.TryParse(reengaged.Response.Text);
            if (emitted is not null) verification = emitted;
            blockedClaim = MasterVerificationParser.TryParseBlockedClaim(reengaged.Response.Text);
            toolCallsInPass = ReengageProgressPolicy.CountToolCalls(reengaged.Response);
        }
        catch (MasterBudgetExhaustedException budgetEx)
        {
            // 2026-09-22-7c41a: the fence's own sentence, which names the cap and the spend.
            logger.LogWarning(
                "Master '{Skill}' hit the cost budget mid re-engagement — stopping (partial work preserved): {Reason}",
                skill, budgetEx.Message);
            return new(new(loopResult, changes, verification), emitted, MasterPassReasons.Budget);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Re-engagement pass failed for master '{Skill}'", skill);
            passThrew = true;
        }

        var after = new ReengageState(loopResult, changes, verification);
        if (s.TicketClarifications?.Captured is not null)
            return new(after, emitted, MasterPassReasons.Question); // asked during the pass — the caller parks

        // p0365: a pass that called NO tool is the one reliable idle signal; p0391: a pass that only
        // re-invented its own checklist and wrote nothing is stopped mechanically.
        var selfRefilled = ReengageProgressPolicy.IsSelfRefilled(
            ledgerIdsAtPassStart, s.Progress.GetLedger(),
            ReengageProgressPolicy.ProducedNewWork(changesAtPassStart, changes));
        var outcome = ReengageProgressPolicy.Decide(toolCallsInPass, blockedClaim, passThrew, selfRefilled);
        if (outcome == ReengageOutcome.Continue) return new(after, emitted, null);
        stopLog.Stopped(skill, outcome, blockedClaim);
        return new(after, emitted, outcome switch
        {
            ReengageOutcome.StopBlocked => MasterPassReasons.Blocked,
            ReengageOutcome.StopSelfRefilled => MasterPassReasons.SelfRefilled,
            _ => MasterPassReasons.Idle,
        });
    }

    // p0411: the run's changed paths, repo-prefixed as the master addresses them.
    private Task<IReadOnlyList<string>> ReadChangedPathsAsync(PipelineContext pipeline, CancellationToken ct) =>
        workingTree.ChangedPathsAsync(
            pipeline.Get<IReadOnlyDictionary<string, ISandbox>>(ContextKeys.Sandboxes),
            pipeline.TryGet<IReadOnlyDictionary<string, string>>(ContextKeys.SandboxRepos, out var keyToRepo) ? keyToRepo : null,
            ct);
}
