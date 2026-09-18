namespace AgentSmith.Domain.Models;

/// <summary>
/// 2026-09-17-0e79c: one premise of a phase that a fresh instance reports does not hold in
/// the repositories as they stand.
/// <para>
/// <see cref="Premise"/> is the claim as the SPEC states it. The checker may paraphrase; the
/// admission resolves what it wrote against the phase's own facts, assumptions and decisions
/// prose and replaces it with the stated wording, so a premise nobody stated can never become a
/// verdict and a verdict always quotes the spec rather than the model.
/// </para>
/// <para>
/// <see cref="Cites"/> is the evidence id of a look the checker itself took, and it is the only
/// thing that can make a finding FALSE: the framework minted that id for a look that reached a
/// verdict, so "this is proven" is never decided by the model labelling its own sentence.
/// <see cref="Evidence"/> is the minted line that id resolves to and <see cref="Looked"/> is
/// what that look actually did — both filled in by the admission, because a reader judging a
/// hand-back has to see the step from the premise to the look and no framework can decide that
/// step for them.
/// </para>
/// </summary>
public sealed record PremiseFinding(
    string Premise,
    string Verdict,
    string Why,
    string? Cites = null,
    string? Evidence = null,
    string? Looked = null);
