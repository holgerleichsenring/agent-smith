using YamlDotNet.Serialization;

namespace AgentSmith.Application.Services.SpecDialog;

/// <summary>
/// p0315e: shared YAML plumbing for the ```outcome block parsers — reads the
/// block into a string-keyed map, pulls typed fields out of it, and turns a
/// nested node back into standalone YAML (so each epic child validates
/// against the phase-spec schema like any other draft). Pure transformation.
/// </summary>
internal static class OutcomeYamlReader
{
    internal static IReadOnlyDictionary<string, object?> ReadMap(string yaml)
    {
        var graph = new DeserializerBuilder().Build().Deserialize<object?>(yaml);
        if (graph is not Dictionary<object, object?> map)
            throw new YamlDotNet.Core.YamlException("the document is not a mapping");
        return map.ToDictionary(e => e.Key.ToString() ?? string.Empty, e => e.Value);
    }

    internal static string? GetString(IReadOnlyDictionary<string, object?> map, string key) =>
        map.TryGetValue(key, out var value) ? value as string : null;

    internal static IReadOnlyDictionary<string, object?>? GetMap(
        IReadOnlyDictionary<string, object?> map, string key) =>
        map.TryGetValue(key, out var value) && value is Dictionary<object, object?> nested
            ? nested.ToDictionary(e => e.Key.ToString() ?? string.Empty, e => e.Value)
            : null;

    internal static IReadOnlyList<object?>? GetList(
        IReadOnlyDictionary<string, object?> map, string key) =>
        map.TryGetValue(key, out var value) && value is List<object?> list ? list : null;

    // 2026-10-02-3f06a: every scalar comes back from the untyped read as a string, so a quoted
    // "42" must be written quoted again — plain, the validator would read it as the number the
    // author did not write.
    internal static string ToYaml(object node) =>
        new SerializerBuilder().WithQuotingNecessaryStrings().Build().Serialize(node);
}
