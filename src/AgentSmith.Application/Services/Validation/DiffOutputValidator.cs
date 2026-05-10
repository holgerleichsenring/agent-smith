using AgentSmith.Application.Services.Loop;

namespace AgentSmith.Application.Services.Validation;

/// <summary>
/// ISkillOutputValidator for output_schema=diff.
/// </summary>
public sealed class DiffOutputValidator : ISkillOutputValidator
{
    private readonly JsonSchemaLoader _loader;

    public DiffOutputValidator(JsonSchemaLoader loader) => _loader = loader;

    public ValidationResult Validate(string output)
        => SchemaValidator.Validate(output, _loader.Get(SkillOutputSchema.Diff), "diff");
}
