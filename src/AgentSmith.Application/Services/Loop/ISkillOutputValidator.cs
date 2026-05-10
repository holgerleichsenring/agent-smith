namespace AgentSmith.Application.Services.Loop;

/// <summary>
/// Validates a skill's textual output against an expected shape (e.g. JSON schema).
/// In p0126 the only built-in implementation is <see cref="NoOpSkillOutputValidator"/>;
/// schema-aware validators land in p0128 alongside Plan/Diff schema persistence.
/// </summary>
public interface ISkillOutputValidator
{
    ValidationResult Validate(string output);
}
