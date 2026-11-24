using YamlDotNet.RepresentationModel;

namespace AgentSmith.Application.Services.Pocs.GitOps;

/// <summary>2026-11-23-c81eb: reads and sets keys of a manifest's mapping nodes — a missing mapping on the way is
/// created, so a render can set a field the template left out.</summary>
public static class PocYamlTree
{
    public static YamlNode? Get(YamlMappingNode map, string key) =>
        map.Children.FirstOrDefault(c => c.Key is YamlScalarNode k && k.Value == key).Value;

    public static string? Text(YamlMappingNode map, string key) => (Get(map, key) as YamlScalarNode)?.Value;

    public static YamlMappingNode? MapOf(YamlMappingNode map, params string[] path)
    {
        YamlMappingNode? node = map;
        foreach (var key in path) node = node is null ? null : Get(node, key) as YamlMappingNode;
        return node;
    }

    /// <summary>The mapping at <paramref name="key"/>, created (or put in place of a non-mapping) when absent.</summary>
    public static YamlMappingNode Map(YamlMappingNode map, string key)
    {
        if (Get(map, key) is YamlMappingNode existing) return existing;
        var created = new YamlMappingNode();
        Set(map, key, created);
        return created;
    }

    public static void Set(YamlMappingNode map, string key, YamlNode value)
    {
        var existing = map.Children.Keys.FirstOrDefault(k => k is YamlScalarNode s && s.Value == key);
        if (existing is not null) map.Children[existing] = value;
        else map.Add(new YamlScalarNode(key), value);
    }

    public static void Remove(YamlMappingNode map, string key)
    {
        if (map.Children.Keys.FirstOrDefault(k => k is YamlScalarNode s && s.Value == key) is { } existing)
            map.Children.Remove(existing);
    }

    public static IEnumerable<YamlMappingNode> Items(YamlMappingNode map, string key) =>
        Get(map, key) is YamlSequenceNode list ? list.Children.OfType<YamlMappingNode>() : [];
}
