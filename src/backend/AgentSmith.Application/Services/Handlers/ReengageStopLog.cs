using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// 2026-10-13-9a59f: the open loop's stop lines, lifted out of <see cref="AgenticMasterHandler"/> with the
/// loop. Each stop is named in the log — the pass record names it too, but the log is what an operator
/// tails.
/// </summary>
public sealed class ReengageStopLog(ILogger<ReengageStopLog> logger)
{
    // 2026-09-22-7c41a: when MONEY ended the re-engagement, say which cap and what was spent.
    // p0406: a contract still unsatisfied by a master that never emitted a verdict is a NAMED outcome —
    // an unknown verdict the keystone records honestly — not a silent break.
    internal void NotReengaging(ReengageSession session, MasterVerification? verification, int reengagePass, int criteriaCount)
    {
        var skill = session.Context.MasterSkillName;
        if (session.CostTracker.IsBudgetExhausted)
            logger.LogWarning("Master '{Skill}' will not re-engage — {Stop}", skill, CostCapStop.Describe(session.CostTracker));
        if (!MasterAcceptanceGate.VerdictlessAfterOneRedrive(
                verification, reengagePass, criteriaCount, session.VerdictOwed.Demanded))
            return;
        logger.LogWarning(
            "Master '{Skill}' emitted no verification verdict across {Passes} pass(es) — ending the "
            + "open loop on an unknown verdict rather than re-driving a null", skill, reengagePass);
    }

    // p0365: an empty pass (no tool call) is surfaced as an idle stop, not a silent truncation; an honest
    // blocked claim with a concrete blocker is respected and named.
    internal void Stopped(string skill, ReengageOutcome outcome, MasterBlockedClaim? block)
    {
        if (outcome == ReengageOutcome.StopBlocked)
            logger.LogInformation(
                "Master '{Skill}' reported a concrete blocker — stopping the open loop (respected): {Blocker}",
                skill, block?.Blocker);
        else if (outcome == ReengageOutcome.StopSelfRefilled)
            logger.LogInformation(
                "Master '{Skill}' re-engagement pass refilled its own checklist — every remaining step was "
                + "added during the pass and nothing reached the diff; stopping the open loop so the run "
                + "ends on its verdict instead of on the budget", skill);
        else
            logger.LogInformation(
                "Master '{Skill}' re-engagement pass called no tool — the model is idle with work still open; "
                + "stopping and surfacing for review", skill);
    }
}
