using System.Collections;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace AgentSmith.Infrastructure.Persistence.Services.SpecRename;

/// <summary>
/// 2026-10-12-967ab/967ah: walks a stored document beside the CLR type that reads it and renames
/// every member (and every enum value) still stored under an old name to the current one, as
/// <see cref="SpecRenameTable"/> lists them per declaring type. A member moves only where the type
/// owning it is listed, so the execution stage an LLM event or a cost record carries is never
/// touched. Running it twice changes nothing — after the first pass no old name is left to find.
/// </summary>
public sealed class SpecMemberRewriter
{
    /// <summary>Rewrites <paramref name="node"/> in place; true when anything changed.</summary>
    public bool Rewrite(JsonNode? node, Type type, JsonNamingPolicy? policy)
    {
        ArgumentNullException.ThrowIfNull(type);
        type = Nullable.GetUnderlyingType(type) ?? type;
        return node switch
        {
            JsonValue value when type.IsEnum => RewriteEnum(value, type),
            JsonArray array => ElementType(type) is { } element && RewriteAll(array, element, policy),
            JsonObject obj when DictionaryValueType(type) is { } valueType =>
                RewriteAll([.. obj.Select(p => p.Value)], valueType, policy),
            JsonObject obj when IsWalkable(type) => RewriteObject(obj, type, policy),
            _ => false,
        };
    }

    private bool RewriteAll(IEnumerable<JsonNode?> nodes, Type type, JsonNamingPolicy? policy)
    {
        var changed = false;
        foreach (var child in nodes.ToList()) changed |= Rewrite(child, type, policy);
        return changed;
    }

    private bool RewriteObject(JsonObject obj, Type type, JsonNamingPolicy? policy)
    {
        var changed = false;
        foreach (var rename in SpecRenameTable.Members.GetValueOrDefault(type) ?? [])
            changed |= Move(obj, Name(rename.Old, policy), Name(rename.New, policy));
        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.GetIndexParameters().Length > 0) continue;
            if (obj.TryGetPropertyValue(JsonName(property, policy), out var child) && child is not null)
                changed |= Rewrite(child, property.PropertyType, policy);
        }
        return changed;
    }

    // The old readers set the alias after the declared member, so a non-null old value wins.
    private static bool Move(JsonObject obj, string old, string current)
    {
        if (!obj.TryGetPropertyValue(old, out var value)) return false;
        obj.Remove(old);
        if (value is not null || !obj.ContainsKey(current)) obj[current] = value;
        return true;
    }

    // An enum stored by name keeps the casing it was written in (a camelCase converter or none).
    private static bool RewriteEnum(JsonValue value, Type type)
    {
        if (!value.TryGetValue<string>(out var text)) return false;
        var rename = (SpecRenameTable.EnumMembers.GetValueOrDefault(type) ?? [])
            .FirstOrDefault(r => string.Equals(r.Old, text, StringComparison.OrdinalIgnoreCase));
        if (rename is null) return false;
        value.ReplaceWith(JsonValue.Create(char.IsLower(text[0]) ? JsonNamingPolicy.CamelCase.ConvertName(rename.New) : rename.New));
        return true;
    }

    private static string Name(string declared, JsonNamingPolicy? policy) => policy?.ConvertName(declared) ?? declared;

    private static string JsonName(PropertyInfo property, JsonNamingPolicy? policy) =>
        property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ?? Name(property.Name, policy);

    private static Type? ElementType(Type type)
    {
        if (type.IsArray) return type.GetElementType();
        if (type == typeof(string) || !typeof(IEnumerable).IsAssignableFrom(type)) return null;
        return Enumerables(type).FirstOrDefault()?.GetGenericArguments()[0];
    }

    private static Type? DictionaryValueType(Type type) =>
        Enumerables(type)
            .Select(e => e.GetGenericArguments()[0])
            .FirstOrDefault(kv => kv.IsGenericType && kv.GetGenericTypeDefinition() == typeof(KeyValuePair<,>))
            ?.GetGenericArguments()[1];

    private static IEnumerable<Type> Enumerables(Type type) =>
        (type.IsInterface ? [type, .. type.GetInterfaces()] : type.GetInterfaces())
            .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));

    // Framework types carry no renamed members; walking them would only cost time.
    private static bool IsWalkable(Type type) =>
        type is { IsPrimitive: false, IsEnum: false } && type != typeof(string) && type != typeof(object)
        && type.Namespace?.StartsWith("System", StringComparison.Ordinal) != true;
}
