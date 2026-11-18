using System.Text.RegularExpressions;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace AgentSmith.Application.Services.Deployment;

/// <summary>
/// 2026-11-18-3863c: reads and replaces the ONE scalar a deployment template names as an environment's version, at a
/// dotted key path (mapping keys, and sequence indexes as numbers: <c>images.0.newTag</c>). The replacement splices the
/// scalar's own characters in the manifest text, so every other byte — comments, order, layout — stays as it was, and
/// the scalar keeps its quoting. A plain scalar whose new value would read as a number or a boolean is double-quoted.
/// </summary>
public sealed partial class ManifestVersionKey
{
    /// <summary>The scalar's value, or null with the reason it cannot be read.</summary>
    public (string? Value, string? Error) Read(string manifest, string key)
    {
        var (node, error) = Find(manifest, key);
        return node is null ? (null, error) : (node.Value, null);
    }

    /// <summary>The manifest with the scalar at <paramref name="key"/> set to <paramref name="value"/>, or the reason.</summary>
    public (string? Manifest, string? Error) Replace(string manifest, string key, string value)
    {
        var (node, error) = Find(manifest, key);
        if (node is null) return (null, error);
        var start = (int)node.Start.Index;
        var end = (int)node.End.Index;
        return (manifest[..start] + Written(node.Style, value) + manifest[end..], null);
    }

    private static (YamlScalarNode? Node, string? Error) Find(string manifest, string key)
    {
        var stream = new YamlStream();
        try { stream.Load(new StringReader(manifest)); }
        catch (YamlException ex) { return (null, $"the manifest is not readable YAML ({ex.Start}: {ex.Message})"); }
        foreach (var document in stream.Documents)
        {
            var node = Walk(document.RootNode, key.Split('.'));
            if (node is YamlScalarNode scalar) return (scalar, null);
            if (node is not null) return (null, $"'{key}' is not a single value in the manifest");
        }
        return (null, $"the manifest has no '{key}'");
    }

    private static YamlNode? Walk(YamlNode node, IEnumerable<string> segments)
    {
        foreach (var segment in segments)
        {
            YamlNode? next = node switch
            {
                YamlMappingNode map => map.Children.FirstOrDefault(c => c.Key is YamlScalarNode k && k.Value == segment).Value,
                YamlSequenceNode list when int.TryParse(segment, out var i) && i >= 0 && i < list.Children.Count => list.Children[i],
                _ => null,
            };
            if (next is null) return null;
            node = next;
        }
        return node;
    }

    private static string Written(ScalarStyle style, string value) => style switch
    {
        ScalarStyle.DoubleQuoted => $"\"{value}\"",
        ScalarStyle.SingleQuoted => $"'{value}'",
        _ => ReadsAsNonString().IsMatch(value) ? $"\"{value}\"" : value,
    };

    [GeneratedRegex(@"^([0-9][0-9._]*|true|false|yes|no|on|off|null|~)$", RegexOptions.IgnoreCase)]
    private static partial Regex ReadsAsNonString();
}
