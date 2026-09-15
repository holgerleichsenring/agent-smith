namespace AgentSmith.Contracts.Models;

/// <summary>
/// p0379: the deterministic composition of the authored universal principles
/// core with one language delta from the skill catalog. Produced by
/// <see cref="Services.IPrinciplesTemplateSource"/>; written verbatim as a
/// component's principles.md by the init-project bootstrap round.
/// </summary>
/// <param name="Content">The full composed markdown (core + delta + project-specifics section).</param>
/// <param name="LanguageSlug">The normalized language slug the delta was resolved for.</param>
/// <param name="DeltaApplied">False when no delta exists for the slug yet — the core composed alone.</param>
/// <param name="Artefacts">2026-09-15-d66f: the enforcement files the delta declares. They are
/// parsed OUT of the delta and are deliberately absent from <paramref name="Content"/> — a
/// principles file that restated them would be a second place for them to disagree, and every
/// stack's composed bytes would change the moment the section became mandatory.</param>
public sealed record ComposedPrinciples(
    string Content,
    string LanguageSlug,
    bool DeltaApplied,
    IReadOnlyList<PrinciplesArtefact>? Artefacts = null)
{
    /// <summary>Never null: a delta that declares none says so, and "none" is an answer.</summary>
    public IReadOnlyList<PrinciplesArtefact> Artefacts { get; init; } = Artefacts ?? [];
}
