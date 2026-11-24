using YamlDotNet.RepresentationModel;

namespace AgentSmith.Application.Services.Pocs.GitOps;

/// <summary>2026-11-23-c81eb: double-quotes every plain scalar a string field of a manifest holds that would read as a
/// number, boolean or null — label, annotation and selector values, ConfigMap and Secret data, and env values — wherever
/// the template put them, so a PoC named 123 stays a string.</summary>
public static class PocStringFields
{
    public static void Requote(YamlMappingNode document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (PocYamlTree.Text(document, "kind") is "ConfigMap" or "Secret")
            foreach (var key in new[] { "data", "stringData" })
                if (PocYamlTree.Get(document, key) is YamlMappingNode data) Values(data);
        Walk(document);
    }

    private static void Walk(YamlNode node)
    {
        if (node is YamlSequenceNode list)
        {
            foreach (var item in list.Children) Walk(item);
            return;
        }
        if (node is not YamlMappingNode map) return;
        foreach (var (key, value) in map.Children)
        {
            var name = (key as YamlScalarNode)?.Value ?? string.Empty;
            if (name is "labels" or "annotations" or "matchLabels" or "selector" && value is YamlMappingNode strings && strings.Children.Values.All(v => v is YamlScalarNode)) Values(strings);
            else if (name == "env" && value is YamlSequenceNode env)
                foreach (var entry in env.Children.OfType<YamlMappingNode>())
                    if (PocYamlTree.Get(entry, "value") is { } envValue) PocYamlText.Requote(envValue);
            Walk(value);
        }
    }

    private static void Values(YamlMappingNode map)
    {
        foreach (var value in map.Children.Values) PocYamlText.Requote(value);
    }
}
