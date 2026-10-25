using AgentSmith.Tests.Architecture;
using YamlDotNet.RepresentationModel;

namespace AgentSmith.Tests.Deploy;

/// <summary>2026-10-25-c5a7c: the shipped deploy/ YAML, read as documents for manifest tests.</summary>
internal static class DeployManifests
{
    public static string Path(params string[] parts) =>
        System.IO.Path.Combine([ArchitectureSources.RepositoryRoot, "deploy", .. parts]);

    public static IReadOnlyList<YamlMappingNode> Documents(params string[] parts)
    {
        var stream = new YamlStream();
        using var reader = new StringReader(File.ReadAllText(Path(parts)));
        stream.Load(reader);
        return [.. stream.Documents.Select(d => d.RootNode).OfType<YamlMappingNode>()];
    }

    public static IReadOnlyList<YamlMappingNode> OfKind(IEnumerable<YamlMappingNode> documents, string kind) =>
        [.. documents.Where(d => Scalar(d, "kind") == kind)];

    public static string? Scalar(YamlNode node, params string[] path)
    {
        var current = node;
        foreach (var key in path)
        {
            if (current is not YamlMappingNode map || !map.Children.TryGetValue(new YamlScalarNode(key), out var next))
                return null;
            current = next;
        }
        return (current as YamlScalarNode)?.Value;
    }

    public static YamlNode? Node(YamlNode node, params string[] path)
    {
        var current = node;
        foreach (var key in path)
        {
            if (current is not YamlMappingNode map || !map.Children.TryGetValue(new YamlScalarNode(key), out var next))
                return null;
            current = next;
        }
        return current;
    }

    public static IEnumerable<YamlMappingNode> Items(YamlNode? sequence) =>
        (sequence as YamlSequenceNode)?.Children.OfType<YamlMappingNode>() ?? [];
}
