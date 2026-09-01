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
public sealed record ComposedPrinciples(
    string Content,
    string LanguageSlug,
    bool DeltaApplied);
