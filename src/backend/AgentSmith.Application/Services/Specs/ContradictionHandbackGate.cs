using AgentSmith.Contracts.Specs;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-10-12-3a36b: stands between a parsed contradiction hand-back and its park. CITES ARE
/// ASKED FOR, NOT FORCED: an uncited contradiction is asked again, and on the last attempt it
/// parks unchecked and says so — it never falls through to the one-spec fallback.
/// 2026-11-18-9c1cb: a cited one parks. No second model reads it: whether a citation resolves is
/// mechanical, whether the claim holds is the person's to answer on the parked ticket.
/// </summary>
public sealed class ContradictionHandbackGate(ILogger<ContradictionHandbackGate> logger)
{
    /// <param name="look">The derivation's own look; null when the run has no sandbox.</param>
    /// <param name="lastAttempt">Whether no attempt follows this one.</param>
    public HandbackGateOutcome Judge(
        SpecDerivation derivation, string? uncited, DerivationLook? look, bool lastAttempt)
    {
        ArgumentNullException.ThrowIfNull(derivation);
        if (derivation.Set.Handback is not { Case: SpecHandbackCase.RequirementsContradictRepository } handback)
            return HandbackGateOutcome.Continue;
        if (look is null)
            return Unchecked("the derivation had no look, so it could cite nothing", handback);
        if (uncited is null) return HandbackGateOutcome.Park;
        return lastAttempt
            ? Unchecked($"the last attempt still cites nothing usable ({uncited})", handback)
            : new HandbackGateOutcome(HandbackGateStep.ReAsk, uncited);
    }

    private HandbackGateOutcome Unchecked(string why, SpecHandback handback)
    {
        logger.LogWarning("The contradiction hand-back parks uncited — {Why}: {Reason}", why, handback.Reason);
        return HandbackGateOutcome.Park;
    }
}
