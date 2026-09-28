using System.Reflection;
using System.Text.Json.Nodes;
using AgentSmith.Infrastructure.Core.Services.Configuration;
using AgentSmith.Tests.Architecture;
using YamlDotNet.Serialization.NamingConventions;

namespace AgentSmith.Tests.Configuration;

/// <summary>
/// Walks a bound type and its schema side by side, the way the loader does: a settable
/// property is a key under its underscored name, a map is <c>additionalProperties</c>, a list
/// is <c>items</c>. Records both directions — a key the loader binds that the schema does not
/// declare, and a key the schema declares that nothing binds.
/// </summary>
internal sealed class ConfigSchemaCoverage
{
    private readonly List<string> _undeclared = [];
    private readonly List<string> _unbound = [];

    public IReadOnlyList<string> Undeclared => _undeclared;
    public IReadOnlyList<string> Unbound => _unbound;

    public static ConfigSchemaCoverage Of(Type root, JsonNode schema, string path = "")
    {
        var coverage = new ConfigSchemaCoverage();
        coverage.WalkObject(root, schema, path);
        return coverage;
    }

    private void WalkObject(Type type, JsonNode schema, string path)
    {
        var declared = ConfigSchemaFile.Properties(schema);
        var bound = BoundKeys(type);
        foreach (var (key, property) in bound)
        {
            var at = Join(path, key);
            if (declared.TryGetValue(key, out var child)) WalkValue(property.PropertyType, child, at);
            else _undeclared.Add(at);
        }
        _unbound.AddRange(declared.Keys.Where(key => !bound.ContainsKey(key)).Select(key => Join(path, key)));
    }

    private void WalkValue(Type type, JsonNode schema, string path)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        if (type.IsEnum) CheckEnum(type, schema, path);
        if (IsLeaf(type)) return;
        if (Generic(type, typeof(IDictionary<,>)) is { } map) WalkChild(map[1], schema, "additionalProperties", path + ".*");
        else if (Generic(type, typeof(IEnumerable<>)) is { } list) WalkChild(list[0], schema, "items", path + "[]");
        else WalkObject(type, schema, path);
    }

    /// <summary>A schema that lists an enum's values must list every value the loader accepts.</summary>
    private void CheckEnum(Type type, JsonNode schema, string path)
    {
        if (ConfigSchemaFile.Parts(schema).Select(p => p["enum"]).FirstOrDefault(e => e is JsonArray) is not JsonArray listed)
            return;
        var declared = listed.Select(v => (string?)v).ToHashSet(StringComparer.Ordinal);
        _undeclared.AddRange(Enum.GetNames(type).Select(name => WireName(type, name))
            .Where(wire => !declared.Contains(wire)).Select(wire => $"{path}={wire}"));
    }

    private static string WireName(Type type, string name) =>
        type.GetField(name)?.GetCustomAttribute<System.Runtime.Serialization.EnumMemberAttribute>()?.Value
        ?? UnderscoredNamingConvention.Instance.Apply(name);

    private void WalkChild(Type type, JsonNode schema, string keyword, string path)
    {
        if (ConfigSchemaFile.Child(schema, keyword) is { } child) WalkValue(type, child, path);
        else _undeclared.Add(path);
    }

    /// <summary>Only a settable property is a key: the deserializer cannot fill the rest.</summary>
    public static IReadOnlyDictionary<string, PropertyInfo> BoundKeys(Type type) =>
        type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.SetMethod is { IsPublic: true } && p.GetIndexParameters().Length == 0)
            .Where(p => p.GetCustomAttribute<YamlDotNet.Serialization.YamlIgnoreAttribute>() is null)
            .ToDictionary(p => UnderscoredNamingConvention.Instance.Apply(p.Name), StringComparer.Ordinal);

    private static bool IsLeaf(Type type) =>
        type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(decimal)
        || type == typeof(TimeSpan) || type == typeof(DateTimeOffset) || type == typeof(Guid)
        || type == typeof(object) || type == typeof(RawRepoRef);

    private static Type[]? Generic(Type type, Type definition) =>
        (type.IsGenericType && type.GetGenericTypeDefinition() == definition ? type : null)
        ?.GetGenericArguments()
        ?? type.GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == definition)
            ?.GetGenericArguments();

    private static string Join(string path, string key) => path.Length == 0 ? key : $"{path}.{key}";
}
