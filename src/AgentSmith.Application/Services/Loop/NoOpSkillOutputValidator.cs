namespace AgentSmith.Application.Services.Loop;

/// <summary>
/// Default validator for p0126: every output is treated as valid. Schema-aware
/// implementations land in p0128 alongside Plan/Diff schema persistence.
/// </summary>
public sealed class NoOpSkillOutputValidator : ISkillOutputValidator
{
    public ValidationResult Validate(string output) => ValidationResult.Valid();
}
