namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-10-12-3a36b: what the contradiction check found. Only a REFUTATION changes anything, and
/// only one resting on a look the checker took itself: everything else lets the hand-back park as
/// it would have.
/// </summary>
/// <param name="IsRefuted">True when the checker's own look shows the claim is not so.</param>
/// <param name="Why">The checker's sentence, for the re-ask and the log.</param>
/// <param name="Evidence">The minted line of the look the refutation rests on; null unless refuted.</param>
public sealed record ContradictionCheck(bool IsRefuted, string Why, string? Evidence = null)
{
    /// <summary>The claim stands, or could not be settled — the hand-back parks.</summary>
    public static ContradictionCheck Stands(string why) => new(false, why);

    /// <summary>The refutation the deriver is answered with, its evidence line included.</summary>
    public string Refutation =>
        $"A second reading of the repository refutes it: {Why} (evidence: {Evidence}). "
        + "Look again at what the ticket asks against what the repository holds now, then cut the "
        + "phases — or hand back only what your own looks show.";
}
