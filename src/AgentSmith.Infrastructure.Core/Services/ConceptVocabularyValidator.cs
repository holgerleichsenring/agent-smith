using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Skills;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Core.Services;

/// <summary>
/// p0131a: load-time concept-vocabulary check is now a no-op. Pre-p0127c
/// versions inspected each skill's <c>activation.positive</c> bag and warned
/// for keys missing from the vocabulary. The bag retired together with the
/// multi-role format; <c>activates_when</c> expressions are validated at
/// build-time by <c>validate-concepts</c>. The class stays as a DI hook so
/// future load-time concept checks can plug in here without re-wiring callers.
/// </summary>
public sealed class ConceptVocabularyValidator(ILogger<ConceptVocabularyValidator> logger)
{
    public void Validate(IReadOnlyList<RoleSkillDefinition> skills, ConceptVocabulary vocabulary)
    {
        // No-op. activates_when validation runs at build-time via the
        // validate-concepts CLI verb (see p0125d).
        _ = skills;
        _ = vocabulary;
        logger.LogTrace("ConceptVocabularyValidator no-op (activates_when checked at build-time).");
    }
}
