using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Skills;

namespace AgentSmith.Infrastructure.Core.Services;

/// <summary>
/// Builds a <see cref="RoleSkillDefinition"/> from new-format SKILL.md
/// frontmatter (p0127a/c). The body after the closing <c>---</c> becomes the
/// rules string verbatim. Validates the frontmatter against the new-format
/// rules before building. p0127c: also populates the legacy
/// <see cref="RoleSkillDefinition.RolesSupported"/> /
/// <see cref="RoleSkillDefinition.RoleAssignments"/> /
/// <see cref="RoleSkillDefinition.OutputContract"/> fields by mapping from
/// the new-format taxonomy so existing triage / index consumers keep working
/// during the [Obsolete] window before p0131 removes them outright.
/// </summary>
internal sealed class NewFormatSkillBuilder(NewFormatSkillValidator validator)
{
    public RoleSkillDefinition Build(
        SkillMdFrontmatter meta, string body, string skillDirectory, string skillFilePath)
    {
        validator.Validate(meta, body, skillFilePath);
        var legacyRole = MapRole(meta.Role!);
        return new RoleSkillDefinition
        {
            Name = meta.Name,
            DisplayName = meta.DisplayName ?? string.Empty,
            Emoji = meta.Emoji ?? string.Empty,
            Description = meta.Description ?? string.Empty,
            Triggers = meta.Triggers ?? [],
            Rules = body.Trim(),
            SkillDirectory = skillDirectory,
            ActivatesWhen = meta.ActivatesWhen!.Trim(),
            Role = meta.Role,
            Category = meta.Category,
            InvestigatorMode = meta.InvestigatorMode,
            SurveyScope = meta.SurveyScope,
            ScopeHint = meta.ScopeHint,
            BlockCondition = meta.BlockCondition,
            Loop = meta.Loop,
            OutputSchema = meta.OutputSchema,
            RolesSupported = [legacyRole],
            RoleAssignments = [new RoleAssignment(legacyRole, ActivationCriteria.Empty)],
            Activation = ActivationCriteria.Empty,
            References = [],
            OutputContract = BuildOutputContract(legacyRole, meta.OutputSchema!),
        };
    }

    private static SkillRole MapRole(string role) => role switch
    {
        "producer" => SkillRole.Lead,
        "investigator" => SkillRole.Analyst,
        "judge" => SkillRole.Reviewer,
        "filter" => SkillRole.Filter,
        _ => SkillRole.Analyst,
    };

    private static OutputContract BuildOutputContract(SkillRole role, string outputSchema)
    {
        var form = outputSchema switch
        {
            "plan" => OutputForm.Plan,
            "diff" => OutputForm.Artifact,
            "bootstrap" => OutputForm.Artifact,
            _ => OutputForm.List,
        };
        return new OutputContract(
            outputSchema,
            MaxObservations: 0,
            MaxCharsPerField: 0,
            new Dictionary<SkillRole, OutputForm> { [role] = form });
    }
}
