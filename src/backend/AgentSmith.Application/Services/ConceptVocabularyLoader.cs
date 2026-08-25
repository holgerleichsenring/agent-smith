using AgentSmith.Contracts.Models.Skills;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services;

/// <summary>
/// p0125c-followup: shared concept vocabulary is loaded from the catalog's
/// <c>skills/</c> subtree. <see cref="ISkillsCatalogPath.Root"/> points at
/// the extracted catalog root (e.g. <c>~/.cache/agentsmith/skills/</c>); the
/// vocab YAML sits at <c>{Root}/skills/concept-vocabulary.yaml</c>.
/// Returns <see cref="ConceptVocabulary.Empty"/> when the catalog isn't
/// bootstrapped yet (CLI tooling running before the resolver wired it up,
/// or dev-from-source with no catalog at all). The empty vocab matches the
/// pre-fix behavior; concept-writers in early steps will throw with the
/// same KeyNotFoundException as before, surfacing the missing-catalog
/// problem clearly rather than masking it.
/// <para>
/// p0515: extracted from ExecutePipelineUseCase — where the vocabulary comes from is not
/// the same reason to change as how a run is executed.
/// </para>
/// </summary>
public sealed class ConceptVocabularyLoader(
    ISkillsCatalogPath catalogPath,
    ISkillLoader skillLoader,
    ILogger<ConceptVocabularyLoader> logger)
{
    /// <summary>
    /// Sub-path inside the catalog root where the shared concept vocabulary lives.
    /// The vocabulary is global per catalog (not per-pipeline-skills-path), so it
    /// always sits at the top of the <c>skills/</c> tree regardless of which
    /// pipeline runs.
    /// </summary>
    public const string CatalogSkillsRootSubPath = "skills";

    public ConceptVocabulary Load()
    {
        try
        {
            var skillsRoot = Path.Combine(catalogPath.Root, CatalogSkillsRootSubPath);
            if (!Directory.Exists(skillsRoot))
            {
                logger.LogWarning(
                    "Skills root {Path} not present at pipeline-bootstrap; concept vocabulary " +
                    "will be empty until LoadSkills repopulates it.", skillsRoot);
                return ConceptVocabulary.Empty;
            }
            return skillLoader.LoadVocabulary(skillsRoot) ?? ConceptVocabulary.Empty;
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex,
                "Catalog not yet bootstrapped at pipeline-start; concept vocabulary " +
                "will be empty until LoadSkills repopulates it.");
            return ConceptVocabulary.Empty;
        }
    }
}
