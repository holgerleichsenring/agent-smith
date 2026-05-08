using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Loop;

namespace AgentSmith.Application.Services.Validation;

/// <summary>
/// ISkillOutputValidator for output_schema=observation. Wraps the existing
/// ObservationParser (p0124) so the resilient extraction stays the runtime authority
/// for the observation contract; the JSON Schema mirror exists only for documentation
/// symmetry with Plan/Diff/Bootstrap. Returns Ok when the parser yields at least one
/// observation, Failure otherwise.
/// </summary>
public sealed class ObservationOutputValidator : ISkillOutputValidator
{
    private const string PlaceholderRole = "validator";

    public ValidationResult Validate(string output)
    {
        var parsed = ObservationParser.TryParseWithoutIds(output, PlaceholderRole);
        return parsed is { Count: > 0 }
            ? ValidationResult.Valid()
            : ValidationResult.Invalid("observation output produced zero parseable observations");
    }
}
