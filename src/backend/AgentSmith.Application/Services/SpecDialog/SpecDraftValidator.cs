using System.Text.RegularExpressions;
using AgentSmith.Application.Services.Validation;
using YamlDotNet.Core;
using YamlDotNet.Serialization;

namespace AgentSmith.Application.Services.SpecDialog;

/// <summary>
/// p0315b: extracts the fenced ```yaml block from a design-partner reply and
/// validates it against the embedded phase-spec schema (YAML → JSON → the
/// same JsonSchema.Net evaluation the skill-output validators use). Pure
/// transformation — the caller decides what a failure means (re-prompt).
/// </summary>
public sealed partial class SpecDraftValidator(PhaseSpecSchemaProvider schemaProvider)
    : ISpecDraftValidator
{
    [GeneratedRegex("```yaml\\s*\\n(.*?)```", RegexOptions.Singleline)]
    private static partial Regex YamlBlockRegex();

    public SpecDraftOutcome Validate(string reply)
    {
        if (string.IsNullOrWhiteSpace(reply)) return new SpecDraftAbsent();

        var blocks = YamlBlockRegex().Matches(reply);
        if (blocks.Count == 0) return new SpecDraftAbsent();
        if (blocks.Count > 1)
            return new SpecDraftInvalid(
                $"the reply contains {blocks.Count} ```yaml blocks — emit exactly one phase-spec draft");

        return ValidateBlock(blocks[0].Groups[1].Value);
    }

    private SpecDraftOutcome ValidateBlock(string yaml)
    {
        string json;
        try
        {
            var graph = new DeserializerBuilder().Build().Deserialize<object?>(yaml);
            if (graph is null)
                return new SpecDraftInvalid("the ```yaml block is empty");
            json = new SerializerBuilder().JsonCompatible().Build().Serialize(graph);
        }
        catch (YamlException ex)
        {
            return new SpecDraftInvalid($"the draft is not valid YAML: {ex.Message}");
        }

        var result = SchemaValidator.Validate(json, schemaProvider.Schema, "phase-spec");
        return result.IsValid
            ? new SpecDraftValid(yaml.Trim())
            : new SpecDraftInvalid(result.ErrorMessage ?? "phase-spec schema validation failed");
    }
}
