namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-09-07-b7e2: how many looks one DERIVATION may take, across every attempt of its
/// retry loop.
/// <para>
/// Its own type rather than <see cref="AccountSearchBudget"/> because the two answer
/// different questions: the account's allowance is per PASS and re-opens for each one,
/// while a derivation is one question asked up to three times — a retry that re-opened
/// the allowance would let a cut that was rejected for its shape spend a fresh budget on
/// looks it has already taken. One allowance, held by the tool host the deriver owns
/// for the call, and never re-opened.
/// </para>
/// </summary>
public sealed class DerivationLookBudget
{
    /// <summary>Looks one derivation may take. A derivation that cannot settle what it
    /// needs to know in this many is guessing, and every look is a sandbox round-trip
    /// inside a model call before any work has started.</summary>
    public const int Allowance = 12;

    /// <summary>What a look past the allowance is told — its own sentence so every tool
    /// and the loop that fences them read the same one.</summary>
    public static readonly string Exhausted =
        $"No look left — a derivation may take {Allowance}. Write the work order on what you have; "
        + "state as an assumption what you could not settle.";

    private int _spent;

    /// <summary>Takes one look from the allowance, or refuses.</summary>
    public bool TryTake() => Interlocked.Increment(ref _spent) <= Allowance;
}
