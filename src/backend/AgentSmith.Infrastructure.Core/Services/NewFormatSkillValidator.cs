using AgentSmith.Infrastructure.Core.Exceptions;

namespace AgentSmith.Infrastructure.Core.Services;

/// <summary>
/// Enforces the new SKILL.md format rules (p0127a, Phase C). Each violation
/// throws <see cref="SkillFormatException"/> carrying the offending file path
/// and a human-readable rule description.
/// </summary>
internal sealed class NewFormatSkillValidator
{
    private static readonly HashSet<string> AllowedRoles = new(StringComparer.Ordinal)
    {
        "producer", "investigator", "judge", "filter", "master",
    };

    private static readonly HashSet<string> AllowedOutputSchemas = new(StringComparer.Ordinal)
    {
        "observation", "plan", "diff", "bootstrap", "discovery",
    };

    private static readonly HashSet<string> AllowedInvestigatorModes = new(StringComparer.Ordinal)
    {
        "verify_hint", "survey", "verify_diff",
    };

    private const int MaxDescriptionChars = 200;

    public void Validate(SkillMdFrontmatter meta, string body, string skillFilePath)
    {
        ValidateRole(meta, skillFilePath);
        ValidateDescription(meta, skillFilePath);
        // p0179a: master skills are cross-pipeline prompt bodies — they bypass
        // the per-role activation / output / investigator / category / block
        // constraints. Only role + description + body are required.
        if (meta.Role == "master")
        {
            ValidateBody(body, skillFilePath);
            return;
        }
        ValidateActivatesWhen(meta, skillFilePath);
        ValidateOutputSchema(meta, skillFilePath);
        ValidateInvestigatorMode(meta, skillFilePath);
        ValidateSurveyScope(meta, skillFilePath);
        ValidateCategory(meta, skillFilePath);
        ValidateBlockCondition(meta, skillFilePath);
        ValidateBootstrapRequiresProducer(meta, skillFilePath);
        ValidateBody(body, skillFilePath);
    }

    private static void ValidateRole(SkillMdFrontmatter meta, string path)
    {
        if (string.IsNullOrWhiteSpace(meta.Role))
            throw new SkillFormatException(path, "role is required and must be non-empty");
        if (!AllowedRoles.Contains(meta.Role))
            throw new SkillFormatException(
                path, $"role must be one of {{producer, investigator, judge, filter}}; got '{meta.Role}'");
    }

    private static void ValidateDescription(SkillMdFrontmatter meta, string path)
    {
        if (string.IsNullOrWhiteSpace(meta.Description))
            throw new SkillFormatException(path, "description is required and must be non-empty");
        if (meta.Description.Length > MaxDescriptionChars)
            throw new SkillFormatException(
                path, $"description must be at most {MaxDescriptionChars} chars; got {meta.Description.Length}");
    }

    private static void ValidateActivatesWhen(SkillMdFrontmatter meta, string path)
    {
        if (string.IsNullOrWhiteSpace(meta.ActivatesWhen))
            throw new SkillFormatException(path, "activates_when is required and must be non-empty");
    }

    private static void ValidateOutputSchema(SkillMdFrontmatter meta, string path)
    {
        if (string.IsNullOrWhiteSpace(meta.OutputSchema))
            throw new SkillFormatException(path, "output_schema is required and must be non-empty");
        if (!AllowedOutputSchemas.Contains(meta.OutputSchema))
            throw new SkillFormatException(
                path,
                $"output_schema must be one of {{observation, plan, diff, bootstrap, discovery}}; got '{meta.OutputSchema}'");
    }

    private static void ValidateInvestigatorMode(SkillMdFrontmatter meta, string path)
    {
        if (meta.Role != "investigator") return;
        if (string.IsNullOrWhiteSpace(meta.InvestigatorMode))
            throw new SkillFormatException(
                path, "investigator_mode is required when role=investigator");
        if (!AllowedInvestigatorModes.Contains(meta.InvestigatorMode))
            throw new SkillFormatException(
                path,
                $"investigator_mode must be one of {{verify_hint, survey, verify_diff}}; got '{meta.InvestigatorMode}'");
    }

    private static void ValidateSurveyScope(SkillMdFrontmatter meta, string path)
    {
        if (meta.InvestigatorMode != "survey") return;
        if (meta.SurveyScope is null || meta.SurveyScope.Count == 0)
            throw new SkillFormatException(
                path, "survey_scope is required and must be non-empty when investigator_mode=survey");
    }

    private static void ValidateCategory(SkillMdFrontmatter meta, string path)
    {
        if (meta.InvestigatorMode != "verify_hint") return;
        if (string.IsNullOrWhiteSpace(meta.Category))
            throw new SkillFormatException(
                path, "category is required and must be non-empty when investigator_mode=verify_hint");
    }

    private static void ValidateBlockCondition(SkillMdFrontmatter meta, string path)
    {
        if (meta.Role != "judge") return;
        // p0151b: block_condition is meaningful only for judges that gate
        // downstream action (plan / gate output schemas). Judges that emit
        // observations defer the action decision to the consumer's policy —
        // forcing block_condition on them is wrong gating.
        if (meta.OutputSchema is null or "observation") return;
        if (string.IsNullOrWhiteSpace(meta.BlockCondition))
            throw new SkillFormatException(
                path, "block_condition is required and must be non-empty when role=judge and output_schema in {plan, gate}");
    }

    private static void ValidateBootstrapRequiresProducer(SkillMdFrontmatter meta, string path)
    {
        if (meta.OutputSchema != "bootstrap") return;
        if (meta.Role != "producer")
            throw new SkillFormatException(
                path, $"output_schema=bootstrap requires role=producer; got role='{meta.Role}'");
    }

    private static void ValidateBody(string body, string path)
    {
        if (string.IsNullOrWhiteSpace(body))
            throw new SkillFormatException(path, "body must be non-empty");
    }
}
