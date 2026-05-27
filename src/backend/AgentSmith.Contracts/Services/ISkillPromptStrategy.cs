using AgentSmith.Contracts.Commands;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// p0147d: Per-round-type prompt-section provider. Replaces the abstract
/// <c>BuildDomainSection</c>/<c>BuildDomainSectionParts</c> hooks on the old
/// SkillRoundHandlerBase. Each implementation models one round-type's domain
/// section (ticket-driven, security-scan, API-security). Injected into the
/// derived handler so the base shrinks to template-method orchestration.
/// </summary>
public interface ISkillPromptStrategy
{
    /// <summary>
    /// Splits the domain section into a stable prefix (cache-friendly across
    /// same-round calls) and a per-skill suffix.
    /// </summary>
    (string Stable, string PerSkill) BuildDomainSectionParts(PipelineContext pipeline);

    /// <summary>Command name used when emitting follow-up SkillRound commands.</summary>
    string SkillRoundCommandName { get; }
}
