namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-09-07-b7e2: how many looks one LOOK may take, across every attempt that uses it.
/// <para>
/// Its own type rather than <see cref="AccountSearchBudget"/> because the two answer
/// different questions: the account's allowance is per PASS and re-opens for each one,
/// while a derivation is one question asked up to three times — a retry that re-opened
/// the allowance would let a cut that was rejected for its shape spend a fresh budget on
/// looks it has already taken. One allowance, held by the tool host the deriver owns
/// for the call, and never re-opened.
/// </para>
/// <para>
/// 2026-09-15-ffa7: the allowance and the refusal are the holder's, not a constant. The
/// cut reviewer keeps nothing between calls, so it is handed a look — and an allowance —
/// per attempt, and six where the derivation has twelve.
/// </para>
/// </summary>
public sealed class DerivationLookBudget(DerivationLookTerms terms)
{
    private int _spent;

    /// <summary>What a look past the allowance is told — its own sentence so every tool
    /// and the loop that fences them read the same one.</summary>
    public string Exhausted { get; } =
        $"No look left — a {terms.Actor} may take {terms.Allowance}. {terms.Settle}";

    /// <summary>Takes one look from the allowance, or refuses.</summary>
    public bool TryTake() => Interlocked.Increment(ref _spent) <= terms.Allowance;
}
