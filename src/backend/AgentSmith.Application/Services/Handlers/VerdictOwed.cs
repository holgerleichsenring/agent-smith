using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Progress;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// 2026-09-08-805f: the handler's side of the ledger-complete brake — the predicate over
/// the live ledger, the demand the governor appends once, the flag the re-engagement
/// predicates read so an in-pass demand is the one verdict re-drive the run allows, and
/// the two log lines carrying the turns counted and the spend since the checklist
/// completed. Complete means non-empty with nothing pending or in progress: a run that
/// planned nothing owes this brake nothing.
/// </summary>
internal sealed class VerdictOwed(
    string skill, Func<ProgressLedger> ledger, Func<decimal> spentUsd, int allowance, ILogger logger)
{
    private bool _wasComplete;
    private decimal _usdAtComplete;

    /// <summary>True once the demand was appended to a running pass of this master.</summary>
    public bool Demanded { get; private set; }

    public bool IsLedgerComplete()
    {
        var current = ledger();
        var complete = !current.IsEmpty && !current.HasActionablePending;
        if (complete && !_wasComplete) _usdAtComplete = spentUsd();
        _wasComplete = complete;
        return complete;
    }

    public string RenderDemand()
    {
        Demanded = true;
        logger.LogWarning(
            "Master '{Skill}' checklist complete for {Turns} tool turn(s) with no verdict — demanding it "
            + "once (${Usd:F2} spent since the checklist completed)",
            skill, allowance, SpentSinceComplete());
        return "Your progress checklist is complete — every step is marked done — and you have kept "
            + "calling tools instead of closing the run. The run is now waiting for your verdict. If the "
            + "checklist does not already record a build and test result, build the project and run the "
            + "automated tests the way the repository defines them, once. Then emit ONLY your final fenced "
            + "```verdict block reflecting the real build/test outcome (status: green | no-tests | failed) "
            + "with the acceptance dispositions. Nothing before or after the block, and no tool call "
            + "after it.\n\n"
            + ProgressLedgerRenderer.Render(ledger());
    }

    public void Stopped(int turnsSinceComplete) =>
        logger.LogWarning(
            "Master '{Skill}' kept calling tools for {Turns} turn(s) after its checklist completed, "
            + "through the verdict demand — ending the pass and surfacing the run for review "
            + "(${Usd:F2} spent since the checklist completed)",
            skill, turnsSinceComplete, SpentSinceComplete());

    private decimal SpentSinceComplete() => spentUsd() - _usdAtComplete;
}
