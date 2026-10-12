using System.Collections;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using AgentSmith.Domain.Models;

namespace AgentSmith.Contracts.Json;

/// <summary>
/// 2026-10-12-967ab: the write side of <see cref="AcceptedNameAttribute"/> for data already
/// stored. It walks a stored document beside the CLR type that reads it and renames every member
/// (and every enum value) found under an accepted name to the declared one — the same attribute
/// <see cref="AcceptedNames"/> reads with, so the rewrite and the reader cannot disagree.
/// <para>
/// TYPE-scoped, never name-scoped: a member is renamed only where the type that owns it declares
/// the alias, so the execution stage an LLM event or a cost record carries is never touched.
/// Running it twice changes nothing — after the first pass no accepted name is left to find.
/// </para>
/// </summary>
public static class AcceptedNameRewriter
{
    /// <summary>Rewrites <paramref name="node"/> in place; true when anything changed.</summary>
    public static bool Rewrite(JsonNode? node, Type type, JsonNamingPolicy? policy)
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

    private static bool RewriteAll(IEnumerable<JsonNode?> nodes, Type type, JsonNamingPolicy? policy)
    {
        var changed = false;
        foreach (var child in nodes.ToList()) changed |= Rewrite(child, type, policy);
        return changed;
    }

    private static bool RewriteObject(JsonObject obj, Type type, JsonNamingPolicy? policy)
    {
        var changed = false;
        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.GetIndexParameters().Length > 0) continue;
            var declared = JsonName(property, policy);
            foreach (var alias in property.GetCustomAttributes<AcceptedNameAttribute>())
                changed |= Move(obj, policy?.ConvertName(alias.Name) ?? alias.Name, declared);
            if (obj.TryGetPropertyValue(declared, out var child) && child is not null)
                changed |= Rewrite(child, property.PropertyType, policy);
        }
        return changed;
    }

    // The reader sets the alias after the declared member, so a non-null alias value wins.
    private static bool Move(JsonObject obj, string alias, string declared)
    {
        if (alias == declared || !obj.TryGetPropertyValue(alias, out var value)) return false;
        obj.Remove(alias);
        if (value is not null || !obj.ContainsKey(declared)) obj[declared] = value;
        return true;
    }

    // An enum stored by name keeps the casing it was written in (a camelCase converter or none).
    private static bool RewriteEnum(JsonValue value, Type type)
    {
        if (!value.TryGetValue<string>(out var text)) return false;
        foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            if (!field.GetCustomAttributes<AcceptedNameAttribute>()
                    .Any(a => string.Equals(a.Name, text, StringComparison.OrdinalIgnoreCase))) continue;
            var declared = char.IsLower(text[0]) ? JsonNamingPolicy.CamelCase.ConvertName(field.Name) : field.Name;
            value.ReplaceWith(JsonValue.Create(declared));
            return true;
        }
        return false;
    }

    private static string JsonName(PropertyInfo property, JsonNamingPolicy? policy) =>
        property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name
        ?? policy?.ConvertName(property.Name) ?? property.Name;

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

    // Framework types carry no accepted names; walking them would only cost time.
    private static bool IsWalkable(Type type) =>
        type is { IsPrimitive: false, IsEnum: false } && type != typeof(string) && type != typeof(object)
        && type.Namespace?.StartsWith("System", StringComparison.Ordinal) != true;
}
