using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using AgentSmith.Application.Services.Validation;
using YamlDotNet.Core;

namespace AgentSmith.Application.Services.SpecDialog;

/// <summary>
/// p0315b: extracts the fenced ```yaml block from a design-partner reply and
/// validates it against the embedded phase-spec schema (YAML → JSON → the
/// same JsonSchema.Net evaluation the skill-output validators use). Pure
/// transformation — the caller decides what a failure means (re-prompt).
/// <para>
/// 2026-08-25-2c7c: the crossing is <see cref="YamlAsJson"/>, so a scalar the author
/// wrote as a number reaches the schema as a number and a rule about it means what it
/// says. The previous crossing stringified every scalar, which made every non-string
/// rule in the schema unenforceable.
/// </para>
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

        return ValidateYaml(blocks[0].Groups[1].Value);
    }

    public SpecDraftOutcome ValidateYaml(string yaml)
    {
        JsonNode? document;
        try
        {
            document = YamlAsJson.Convert(yaml);
        }
        catch (YamlException ex)
        {
            return new SpecDraftInvalid($"the draft is not valid YAML: {ex.Message}");
        }

        if (document is null) return new SpecDraftInvalid("the ```yaml block is empty");

        var result = SchemaValidator.Validate(document, schemaProvider.Schema, "phase-spec");
        return result.IsValid
            ? new SpecDraftValid(yaml.Trim())
            : new SpecDraftInvalid(result.ErrorMessage ?? "phase-spec schema validation failed");
    }
}
