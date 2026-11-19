using AgentSmith.Application.Services.Prompts;
using AgentSmith.Contracts.Specs;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-10-12-3a36b: the state of one derivation's retry loop — how many attempts it may take,
/// the last rejection it answered, and the uncited contradiction it asked about again.
/// 2026-11-18-9c1cb: no cut review keeps a best cut and nothing refutes a hand-back.
/// </summary>
internal sealed class DerivationAttempts(int max)
{
    private SpecDerivation? _uncitedHandback;

    /// <summary>Set once a cut left a named context uncovered and was pinned back.</summary>
    public bool Pinned { get; private set; }

    public string? LastError { get; private set; }

    public int Bound => max + (Pinned ? 1 : 0);

    public bool IsLast(int attempt) => attempt >= Bound;

    /// <summary>Records the rejection and returns the message the deriver is answered with.</summary>
    public ChatMessage Reject(string verdict, string? error)
    {
        LastError = error;
        return SpecDerivationMessages.Again(verdict, error);
    }

    /// <summary>2026-09-08-1830: pins a cut that left named contexts uncovered back once, with the
    /// contexts it carried in view; the extra attempt this buys is part of <see cref="Bound"/>.</summary>
    public ChatMessage Pin(IReadOnlyList<string> gap, SpecSet cut)
    {
        Pinned = true;
        return new ChatMessage(ChatRole.User,
            ScopedContextGapPromptSection.Render(gap, ScopedContextCoverage.Carried(cut)));
    }

    /// <summary>A contradiction the gate did not let park: asked again.</summary>
    public ChatMessage Gated(HandbackGateOutcome outcome, SpecDerivation derivation)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        if (outcome.Step == HandbackGateStep.ReAsk) _uncitedHandback = derivation;
        return Reject("Your hand-back was not accepted", outcome.Message);
    }

    /// <summary>What the loop answers once the attempts ran out: an uncited contradiction — which parks
    /// rather than falling through to the fallback — else the last rejection.</summary>
    public (SpecDerivation? Derivation, string? Error) Exhausted =>
        _uncitedHandback is not null ? (_uncitedHandback, null) : (null, LastError);
}
