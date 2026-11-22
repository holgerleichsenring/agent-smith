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
    // 2026-11-22-96c8a: checklist steps still open after their one re-drive are a NAMED stop, not a silent break.
    internal void NotReengaging(ReengageSession session, int reengagePass)
    {
        var skill = session.Context.MasterSkillName;
        if (session.CostTracker.IsBudgetExhausted)
            logger.LogWarning("Master '{Skill}' will not re-engage — {Stop}", skill, CostCapStop.Describe(session.CostTracker));
        else if (session.Progress.GetLedger().HasActionablePending && ReengageReasonPolicy.OpenAfterOneRedrive(reengagePass))
            logger.LogWarning(
                "Master '{Skill}' left checklist steps open across {Passes} pass(es) — ending the open loop rather than "
                + "re-driving the same answer", skill, reengagePass);
    }

    // p0365: an empty pass (no tool call) is surfaced as an idle stop, not a silent truncation.
    internal void Stopped(string skill, ReengageOutcome outcome)
    {
        if (outcome == ReengageOutcome.StopSelfRefilled)
            logger.LogInformation(
                "Master '{Skill}' re-engagement pass refilled its own checklist — every remaining step was "
                + "added during the pass and nothing reached the diff; stopping the open loop so the run "
                + "ends on its report instead of on the budget", skill);
        else
            logger.LogInformation(
                "Master '{Skill}' re-engagement pass called no tool — the model is idle with work still open; "
                + "stopping and surfacing for review", skill);
    }
}
