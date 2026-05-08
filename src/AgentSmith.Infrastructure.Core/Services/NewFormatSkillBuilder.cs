using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Infrastructure.Core.Services;

/// <summary>
/// Builds a <see cref="RoleSkillDefinition"/> from new-format SKILL.md
/// frontmatter (p0127a). Bypasses <see cref="SkillBodySplitter"/> — the body
/// after the closing <c>---</c> becomes the rules string verbatim. Validates
/// the frontmatter against the new-format rules before building.
/// </summary>
internal sealed class NewFormatSkillBuilder(NewFormatSkillValidator validator)
{
    public RoleSkillDefinition Build(
        SkillMdFrontmatter meta, string body, string skillDirectory, string skillFilePath)
    {
        validator.Validate(meta, body, skillFilePath);
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
        };
    }
}
