using System.Text.Json.Nodes;
using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Infrastructure.Core.Services.Configuration.Studio;
using YamlDotNet.RepresentationModel;

namespace AgentSmith.Infrastructure.Core.Services.Configuration.Retired;

/// <summary>
/// Reads a configuration as an UNTYPED tree — every key it carries, including the ones no
/// model binds any more. A typed read drops exactly the keys a retired-key check is looking for.
/// Both sources become the same tree, rooted at the YAML root keys: a stored document of a
/// catalog type sits under its collection key and its id, a singleton under its own type.
/// </summary>
public sealed class RawConfigTreeReader
{
    private static readonly IReadOnlyDictionary<string, string> CollectionRoots =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [ConfigDocTypes.Agent] = "agents",
            [ConfigDocTypes.Tracker] = "trackers",
            [ConfigDocTypes.Connection] = "connections",
            [ConfigDocTypes.Repo] = "repos",
            [ConfigDocTypes.Project] = "projects",
            [ConfigDocTypes.McpServer] = "mcp_servers",
            [ConfigDocTypes.DesignSource] = "design_sources",
            [ConfigDocTypes.Secret] = "secrets",
            [ConfigDocTypes.PipelineTrigger] = "pipeline_triggers",
        };

    /// <summary>The tree of a YAML configuration file; an empty file is an empty tree.</summary>
    public JsonNode? FromYaml(string yaml)
    {
        var stream = new YamlStream();
        stream.Load(new StringReader(yaml));
        return stream.Documents.Count == 0 ? null : Convert(stream.Documents[0].RootNode);
    }

    /// <summary>The tree of the stored entity documents, whatever types they are — including a
    /// type the taxonomy no longer assembles.</summary>
    public JsonNode FromStoredDocuments(IReadOnlyList<ConfigDocRow> rows)
    {
        var root = new JsonObject();
        foreach (var row in rows)
        {
            var doc = JsonNode.Parse(row.Doc);
            if (!CollectionRoots.TryGetValue(row.Type, out var collection)) root[row.Type] = doc;
            else ((root[collection] ??= new JsonObject()).AsObject())[row.Id] = doc;
        }
        return root;
    }

    private static JsonNode? Convert(YamlNode node) => node switch
    {
        YamlMappingNode map => Map(map),
        YamlSequenceNode list => new JsonArray([.. list.Children.Select(Convert)]),
        YamlScalarNode scalar => IsNull(scalar) ? null : JsonValue.Create(scalar.Value),
        _ => null,
    };

    private static JsonObject Map(YamlMappingNode map)
    {
        var obj = new JsonObject();
        foreach (var (key, value) in map.Children)
            if (key is YamlScalarNode { Value: { } name }) obj[name] = Convert(value);
        return obj;
    }

    private static bool IsNull(YamlScalarNode scalar) =>
        scalar.Style == YamlDotNet.Core.ScalarStyle.Plain
        && scalar.Value is null or "" or "~" or "null" or "Null" or "NULL";
}
