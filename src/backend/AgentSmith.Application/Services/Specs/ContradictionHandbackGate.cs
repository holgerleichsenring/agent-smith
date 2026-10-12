using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Specs;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-10-12-3a36b: stands between a parsed contradiction hand-back and its park. CITES ARE
/// ASKED FOR, NOT FORCED: an uncited contradiction is asked again, and on the last attempt it
/// parks unchecked and says so — it never falls through to the one-spec fallback. A cited one is
/// put to <see cref="IContradictionChecker"/> over the cited looks plus a fresh look of its own;
/// a refutation buys one more attempt, and a second contradiction after it is not checked again.
/// No look, no check — logged.
/// </summary>
public sealed class ContradictionHandbackGate(
    IContradictionChecker checker,
    DerivationLookFactory looks,
    ILogger<ContradictionHandbackGate> logger)
{
    /// <param name="look">The derivation's own look; null when the run has no sandbox.</param>
    /// <param name="lastAttempt">Whether no attempt follows this one.</param>
    /// <param name="refutedBefore">Whether a contradiction of this derivation was refuted already.</param>
    public async Task<HandbackGateOutcome> JudgeAsync(
        SpecDerivation derivation, string? uncited, DerivationLook? look, bool lastAttempt,
        bool refutedBefore, AgentConfig agent, PipelineContext pipeline, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(derivation);
        if (derivation.Set.Handback is not { Case: SpecHandbackCase.RequirementsContradictRepository } handback)
            return HandbackGateOutcome.Continue;
        if (look is null)
            return Unchecked("the derivation had no look, so there is nothing to check it against", handback);
        if (uncited is not null)
            return lastAttempt
                ? Unchecked($"the last attempt still cites nothing usable ({uncited})", handback)
                : new HandbackGateOutcome(HandbackGateStep.ReAsk, uncited);
        if (refutedBefore)
            return Unchecked("it follows a refuted one, and a second contradiction is not checked again", handback);
        return await CheckAsync(derivation.Set.Key, handback, look, agent, pipeline, ct);
    }

    private async Task<HandbackGateOutcome> CheckAsync(
        string key, SpecHandback handback, DerivationLook look, AgentConfig agent,
        PipelineContext pipeline, CancellationToken ct)
    {
        await using var own = looks.OnTerms(pipeline, DerivationLookTerms.ContradictionCheck);
        if (own is null) return Unchecked("the check has no sandbox to look into", handback);
        var ids = handback.Cites.Select(DerivationEvidence.IdOf).OfType<string>().ToHashSet(StringComparer.Ordinal);
        var cited = look.Evidence.Looks.Where(l => ids.Contains(l.Id)).ToList();
        var check = await checker.CheckAsync(
            handback, cited, own, key, agent, PipelineCostTracker.GetOrCreate(pipeline), ct);
        if (!check.IsRefuted)
        {
            logger.LogInformation("The contradiction hand-back of {Key} stands: {Why}", key, check.Why);
            return HandbackGateOutcome.Park;
        }
        logger.LogWarning("The contradiction hand-back of {Key} was refuted: {Why} ({Evidence})",
            key, check.Why, check.Evidence);
        return new HandbackGateOutcome(HandbackGateStep.Refuted, check.Refutation);
    }

    private HandbackGateOutcome Unchecked(string why, SpecHandback handback)
    {
        logger.LogWarning("The contradiction hand-back parks unchecked — {Why}: {Reason}", why, handback.Reason);
        return HandbackGateOutcome.Park;
    }
}
