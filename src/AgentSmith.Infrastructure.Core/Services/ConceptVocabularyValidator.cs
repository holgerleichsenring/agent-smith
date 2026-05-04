using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Skills;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Core.Services;

/// <summary>
/// Validates loaded skills against the concept vocabulary. Logs a warning for every
/// activation.positive key that is not declared in skills/concept-vocabulary.yaml.
/// Never fails the load — vocabulary is operator-extensible and skill rollouts may
/// legitimately precede a vocabulary update.
/// </summary>
public sealed class ConceptVocabularyValidator(ILogger<ConceptVocabularyValidator> logger)
{
    public void Validate(IReadOnlyList<RoleSkillDefinition> skills, ConceptVocabulary vocabulary)
    {
        var unknownByKey = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        foreach (var skill in skills)
        {
            if (skill.Activation?.Positive is null) continue;
            foreach (var key in skill.Activation.Positive)
            {
                if (vocabulary.TryGet(key.Key, out _)) continue;
                if (!unknownByKey.TryGetValue(key.Key, out var skillsForKey))
                {
                    skillsForKey = [];
                    unknownByKey[key.Key] = skillsForKey;
                }
                skillsForKey.Add(skill.Name);
            }
        }

        foreach (var (key, skillsForKey) in unknownByKey)
        {
            logger.LogWarning(
                "Activation key '{Key}' referenced by {Count} skill(s) [{Skills}] is not in concept-vocabulary.yaml",
                key, skillsForKey.Count, string.Join(", ", skillsForKey));
        }
    }
}
