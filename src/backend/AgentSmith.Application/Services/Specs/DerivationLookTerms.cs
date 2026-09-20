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
/// <param name="NumberedReads">2026-09-17-042eh: whether a file read comes back with a line
/// number in front of every line. A holder that reports a LINE needs them — the read tool
/// returns raw content, so an unnumbered line number is a figure nobody can check — and a
/// holder that only quotes text does not, and pays no tokens for them.</param>
/// <param name="MayRunAStage">2026-09-20-9c74: whether this holder may RUN one verify stage a
/// repository declared, by label. The tool itself is withheld by the collaborator — a look
/// built without one has none, whatever its terms say — and this term is what the prompt
/// sentence is rendered from, so a holder knows whether it may ask. Both, so a mistake in
/// either one fails closed. Every holder names its own value: one of the five is defined AS
/// another with fields replaced, and a default would reach it silently.</param>
public sealed record DerivationLookTerms(
    string Actor, int Allowance, string EvidencePrefix, string Settle, string CiteRule,
    bool NumberedReads = false, bool MayRunAStage = false)
{
    /// <summary>Looks one derivation may take, across every attempt of its retry loop.</summary>
    public const int DerivationAllowance = 12;

    /// <summary>Looks one cut review may take. It checks the premises of a cut that exists
    /// rather than deciding what to build, and it is asked once per derivation ATTEMPT.</summary>
    public const int CutReviewAllowance = 6;

    public static DerivationLookTerms Derivation { get; } = new(
        "derivation", DerivationAllowance, "L",
        "Write the work order on what you have; state as an assumption what you could not settle.",
        "a fact you state cites that id, and a fact that cites none is recorded as an assumption.",
        MayRunAStage: false);

    public static DerivationLookTerms CutReview { get; } = new(
        "cut review", CutReviewAllowance, "R",
        "Judge the cut on what you have; report no false premise you did not look at.",
        "a false premise you report cites that id in \"cites\", and one that cites none is discarded.",
        MayRunAStage: false);

    /// <summary>2026-09-17-042ed: a design turn's proposal, reviewed inside the turn. The cut
    /// review's allowance and rules, under its own name and letter.</summary>
    public static DerivationLookTerms ProposalReview { get; } =
        CutReview with { Actor = "proposal review", EvidencePrefix = "P", MayRunAStage = false };

    /// <summary>2026-09-17-0e79c: the check a phase's own stated premises are put to before
    /// its work starts. Its own letter M — L is the derivation's and P is the proposal
    /// review's — and the cut review's allowance, for the same reason: it checks the premises
    /// of a spec that exists rather than deciding what to build. Its rule differs in what a
    /// citation BUYS: there is no quote to fall back on, so a premise reported without an id
    /// the framework minted is recorded unproven rather than discarded.</summary>
    public static DerivationLookTerms PremiseCheck { get; } = new(
        "premise check", CutReviewAllowance, "M",
        "Judge the premises on what you have; report as unproven any you could not look at.",
        "a premise you report as no longer holding cites that id in \"cites\", and one that cites none is recorded unproven.",
        MayRunAStage: true);

    /// <summary>2026-09-17-042eh: looks one phase review may take. Eight against the cut
    /// review's six: it judges a DIFF, so every file it reports on is a file it must open,
    /// and a phase touching a handful of files exhausts six before it has read them all.</summary>
    public const int PhaseReviewAllowance = 8;

    /// <summary>
    /// 2026-09-17-042eh: the look a verified phase's own diff is reviewed with. Its own name,
    /// and the letter <see cref="ProposalReview"/> also mints under — which is safe only because
    /// no conversation holds both: a proposal is reviewed inside a design turn and a phase diff
    /// inside a code run. What it must not share is the DERIVER's "L", whose ids travel back
    /// into the deriver's own history. Its reads are numbered, because a finding names a line.
    /// </summary>
    public static DerivationLookTerms PhaseReview { get; } = new(
        "phase review", PhaseReviewAllowance, "P",
        "Report only on the files you did read; say nothing about the rest.",
        "a finding you report cites that id in \"cites\", and one that cites none is discarded.",
        NumberedReads: true, MayRunAStage: false);
}
