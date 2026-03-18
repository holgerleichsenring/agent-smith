using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;
using YamlDotNet.RepresentationModel;

namespace AgentSmith.Infrastructure.Core.Services;

/// <summary>
/// Validates generated .context.yaml against the CCS structure.
/// Checks required sections and fields without a full JSON Schema validator.
/// </summary>
public sealed class ContextValidator : IContextValidator
{
    public ContextValidationResult Validate(string yaml)
    {
        if (string.IsNullOrWhiteSpace(yaml))
            return ContextValidationResult.Failure(["YAML content is empty"]);

        YamlMappingNode root;
        try
        {
            var yamlStream = new YamlStream();
            yamlStream.Load(new StringReader(yaml));

            if (yamlStream.Documents.Count == 0)
                return ContextValidationResult.Failure(["No YAML documents found"]);

            root = yamlStream.Documents[0].RootNode as YamlMappingNode
                   ?? throw new InvalidCastException("Root node is not a mapping");
        }
        catch (Exception ex) when (ex is YamlDotNet.Core.YamlException or InvalidCastException)
        {
            return ContextValidationResult.Failure([$"Invalid YAML: {ex.Message}"]);
        }

        var errors = new List<string>();

        ValidateRequiredSection(root, "meta", errors);
        ValidateRequiredSection(root, "stack", errors);
        ValidateRequiredSection(root, "arch", errors);
        ValidateRequiredSection(root, "quality", errors);
        ValidateRequiredSection(root, "state", errors);

        if (TryGetMapping(root, "meta") is { } meta)
        {
            ValidateRequiredField(meta, "meta", "project", errors);
            ValidateRequiredField(meta, "meta", "version", errors);
            ValidateRequiredField(meta, "meta", "type", errors);
            ValidateRequiredField(meta, "meta", "purpose", errors);
        }

        if (TryGetMapping(root, "stack") is { } stack)
        {
            ValidateRequiredField(stack, "stack", "runtime", errors);
            ValidateRequiredField(stack, "stack", "lang", errors);
        }

        if (TryGetMapping(root, "arch") is { } arch)
        {
            ValidateRequiredField(arch, "arch", "style", errors);
            ValidateRequiredField(arch, "arch", "layers", errors);
        }

        if (TryGetMapping(root, "state") is { } state)
        {
            ValidateRequiredField(state, "state", "done", errors);
            ValidateRequiredField(state, "state", "active", errors);
        }

        return errors.Count == 0
            ? ContextValidationResult.Success()
            : ContextValidationResult.Failure(errors);
    }

    private static void ValidateRequiredSection(
        YamlMappingNode root, string sectionName, List<string> errors)
    {
        var key = new YamlScalarNode(sectionName);
        if (!root.Children.ContainsKey(key))
            errors.Add($"Missing required section: '{sectionName}'");
    }

    private static void ValidateRequiredField(
        YamlMappingNode section, string sectionName, string fieldName, List<string> errors)
    {
        var key = new YamlScalarNode(fieldName);
        if (!section.Children.ContainsKey(key))
            errors.Add($"Missing required field: '{sectionName}.{fieldName}'");
    }

    private static YamlMappingNode? TryGetMapping(YamlMappingNode root, string key)
    {
        var yamlKey = new YamlScalarNode(key);
        return root.Children.TryGetValue(yamlKey, out var node)
            ? node as YamlMappingNode
            : null;
    }
}
