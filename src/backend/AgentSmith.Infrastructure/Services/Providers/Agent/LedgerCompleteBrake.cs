using Microsoft.Extensions.AI;

namespace AgentSmith.Infrastructure.Services.Providers.Agent;

/// <summary>
/// 2026-09-08-805f: the per-pass counter behind the ledger-complete brake. A master whose
/// checklist is complete owes its verdict; the idle stop cannot see a master that keeps
/// calling read-only tools, and the money fence is the only other thing that ends such a
/// pass — which turns a finished phase into a failed run. This counts the model's tool
/// turns while the ledger stays complete: after the allowance it asks for the verdict
/// once, after the allowance again it ends the pass. A ledger that stops being complete
/// (an item reopened) resets the count; the demand is made once per pass.
/// <para>
/// The ledger decides that a verdict is owed, never what it is: the brake only ends a
/// pass, and the pass it ends carries no verdict — the run surfaces on an unknown one.
/// </para>
/// </summary>
public sealed class LedgerCompleteBrake(int allowance)
{
    /// <summary>The assistant turn a stopped pass ends on — no provider call is made for it.</summary>
    public const string EndOfPassText =
        "The run ended this pass: the progress checklist was complete, the verdict was demanded "
        + "once, and tool calls continued instead of it. No verdict was emitted.";

    private bool _wasComplete;
    private bool _demanded;
    private int _sinceComplete;
    private int _sinceDemand;

    /// <summary>The model's tool turns since the checklist last became complete.</summary>
    public int TurnsSinceComplete => _sinceComplete;

    /// <summary>
    /// Called once per governor iteration, BEFORE the model is called, with the live
    /// completeness of the ledger. Every iteration after the first follows a tool-calling
    /// turn by construction, so an iteration observed while the ledger was already complete
    /// on the previous one is a turn the model spent with a complete checklist.
    /// </summary>
    public LedgerBrakeAction Observe(bool ledgerComplete)
    {
        if (allowance <= 0 || !ledgerComplete)
        {
            _wasComplete = false;
            _sinceComplete = 0;
            _sinceDemand = 0;
            return LedgerBrakeAction.None;
        }
        if (_wasComplete)
        {
            _sinceComplete++;
            if (_demanded) _sinceDemand++;
        }
        _wasComplete = true;
        if (!_demanded && _sinceComplete >= allowance)
        {
            _demanded = true;
            return LedgerBrakeAction.Demand;
        }
        return _demanded && _sinceDemand >= allowance ? LedgerBrakeAction.Stop : LedgerBrakeAction.None;
    }

    /// <summary>The synthetic, labelled tool-less turn that ends a stopped pass.</summary>
    public static ChatResponse EndOfPass() =>
        new(new ChatMessage(ChatRole.Assistant, EndOfPassText));
}
