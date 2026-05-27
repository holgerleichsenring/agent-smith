using AgentSmith.Application.Services.Loop;

namespace AgentSmith.Application.Services.Validation;

/// <summary>
/// ISkillOutputValidator for output_schema=bootstrap.
/// </summary>
public sealed class BootstrapOutputValidator : ISkillOutputValidator
{
    private readonly JsonSchemaLoader _loader;

    public BootstrapOutputValidator(JsonSchemaLoader loader) => _loader = loader;

    public ValidationResult Validate(string output)
        => SchemaValidator.Validate(output, _loader.Get(SkillOutputSchema.Bootstrap), "bootstrap");
}
