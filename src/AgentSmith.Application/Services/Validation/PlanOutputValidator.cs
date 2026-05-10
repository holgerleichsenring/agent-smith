using AgentSmith.Application.Services.Loop;

namespace AgentSmith.Application.Services.Validation;

/// <summary>
/// ISkillOutputValidator for output_schema=plan. Defers to the cached
/// plan.schema.json via SchemaValidator; surfaces JSON Pointer + rule
/// description in the failure message for RetryCoordinator's retry hint.
/// </summary>
public sealed class PlanOutputValidator : ISkillOutputValidator
{
    private readonly JsonSchemaLoader _loader;

    public PlanOutputValidator(JsonSchemaLoader loader) => _loader = loader;

    public ValidationResult Validate(string output)
        => SchemaValidator.Validate(output, _loader.Get(SkillOutputSchema.Plan), "plan");
}
