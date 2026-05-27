using AgentSmith.Application.Services.Loop;

namespace AgentSmith.Application.Services.Validation;

/// <summary>
/// p0161d: ISkillOutputValidator for output_schema=discovery. Validates the
/// project-discovery skill's JSON output (status + components[] +
/// optional ambiguity) against the embedded discovery.schema.json.
/// </summary>
public sealed class DiscoveryOutputValidator : ISkillOutputValidator
{
    private readonly JsonSchemaLoader _loader;

    public DiscoveryOutputValidator(JsonSchemaLoader loader) => _loader = loader;

    public ValidationResult Validate(string output)
        => SchemaValidator.Validate(output, _loader.Get(SkillOutputSchema.Discovery), "discovery");
}
