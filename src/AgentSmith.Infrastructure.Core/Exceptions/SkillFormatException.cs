using AgentSmith.Domain.Exceptions;

namespace AgentSmith.Infrastructure.Core.Exceptions;

/// <summary>
/// Thrown when a SKILL.md file violates the new-format frontmatter rules
/// (p0127a). Carries the offending file path and a description of the rule
/// that was violated so operators can fix the file directly.
/// </summary>
public sealed class SkillFormatException : AgentSmithException
{
    public SkillFormatException(string skillFilePath, string ruleDescription)
        : base($"[{skillFilePath}] {ruleDescription}")
    {
        SkillFilePath = skillFilePath;
        RuleDescription = ruleDescription;
    }

    public string SkillFilePath { get; }

    public string RuleDescription { get; }
}
