namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-10-02-3f06b: what a caller of <see cref="EvidenceCheck"/> counts as a problem. One reader,
/// two policies: the product accepts minted look lines and reports what it cannot route as not
/// checked; this repository refuses both, because its specs are written by hand against its own
/// tree.
/// </summary>
/// <param name="UncheckedQualifiers">Qualifiers accepted without resolving — repositories the
/// caller knows of but does not hold.</param>
/// <param name="RefusedPathPrefixes">Paths that are not evidence wherever they resolve.</param>
public sealed record EvidencePolicy(
    bool AllowMinted,
    bool NoReferenceIsProblem,
    bool UnknownQualifierIsProblem,
    IReadOnlyList<string> UncheckedQualifiers,
    IReadOnlyList<string> RefusedPathPrefixes,
    bool ObservedNeedsDate)
{
    /// <summary>
    /// This repository's rule. A plan is not evidence: a planned or active phase file moves when
    /// done and states what does not exist yet, so a fact about one cites a dated observation.
    /// </summary>
    public static EvidencePolicy Repository { get; } = new(
        AllowMinted: false,
        NoReferenceIsProblem: true,
        UnknownQualifierIsProblem: true,
        UncheckedQualifiers: ["agent-smith-skills", "spec-first"],
        RefusedPathPrefixes: [".agentsmith/specs/planned/", ".agentsmith/specs/active/"],
        ObservedNeedsDate: true);

    /// <summary>
    /// 2026-10-02-3f06c: a design turn's rule. A minted look line is the partner's own evidence; an
    /// observation needs no date, because the partner is never taught to write one; a fact with no
    /// reference, and every qualified path, is not checked rather than reported. Nothing is refused.
    /// </summary>
    public static EvidencePolicy Product { get; } = new(
        AllowMinted: true,
        NoReferenceIsProblem: false,
        UnknownQualifierIsProblem: false,
        UncheckedQualifiers: [],
        RefusedPathPrefixes: [],
        ObservedNeedsDate: false);
}
