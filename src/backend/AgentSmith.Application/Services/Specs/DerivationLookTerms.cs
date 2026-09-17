namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-09-15-ffa7: whose look this is and what its holder is told — the allowance, the
/// letter its evidence ids are minted under, the name its evidence lines and log lines
/// give it, and the two sentences that change with the reader. The derivation's terms are
/// the ones every look had before a second holder existed; the cut review's are its own,
/// because its ids go back into the DERIVER's conversation, where an L-id already names a
/// look the deriver took.
/// </summary>
/// <param name="Actor">The holder's name, as a noun: "derivation", "cut review".</param>
/// <param name="Allowance">Looks the holder may take on this look.</param>
/// <param name="EvidencePrefix">The letter its evidence ids start with.</param>
/// <param name="Settle">What a look past the allowance is told to do instead.</param>
/// <param name="CiteRule">How a statement resting on a look cites it.</param>
public sealed record DerivationLookTerms(
    string Actor, int Allowance, string EvidencePrefix, string Settle, string CiteRule)
{
    /// <summary>Looks one derivation may take, across every attempt of its retry loop.</summary>
    public const int DerivationAllowance = 12;

    /// <summary>Looks one cut review may take. It checks the premises of a cut that exists
    /// rather than deciding what to build, and it is asked once per derivation ATTEMPT.</summary>
    public const int CutReviewAllowance = 6;

    public static DerivationLookTerms Derivation { get; } = new(
        "derivation", DerivationAllowance, "L",
        "Write the work order on what you have; state as an assumption what you could not settle.",
        "a fact you state cites that id, and a fact that cites none is recorded as an assumption.");

    public static DerivationLookTerms CutReview { get; } = new(
        "cut review", CutReviewAllowance, "R",
        "Judge the cut on what you have; report no false premise you did not look at.",
        "a false premise you report cites that id in \"cites\", and one that cites none is discarded.");

    /// <summary>2026-09-17-042ed: a design turn's proposal, reviewed inside the turn. The cut
    /// review's allowance and rules, under its own name and letter.</summary>
    public static DerivationLookTerms ProposalReview { get; } =
        CutReview with { Actor = "proposal review", EvidencePrefix = "P" };
}
