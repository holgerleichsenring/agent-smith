using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using AgentSmith.Domain.Models;

namespace AgentSmith.Contracts.Json;

/// <summary>
/// 2026-10-12-967aa: the read side of <see cref="AcceptedNameAttribute"/>. A JSON seam reads
/// aliases by taking <see cref="Resolver"/> as its type-info resolver; an enum stored by name is
/// read through <see cref="TryParse{TEnum}"/>. Serialisation is untouched — an alias property has
/// no getter, so nothing a writer emits changes.
/// </summary>
public static class AcceptedNames
{
    /// <summary>The default reflection resolver plus one setter-only property per alias.</summary>
    public static IJsonTypeInfoResolver Resolver { get; } =
        new DefaultJsonTypeInfoResolver { Modifiers = { AddAliases } };

    /// <summary>The serializer defaults (PascalCase, case-sensitive), reading aliases too.</summary>
    public static JsonSerializerOptions DefaultOptions { get; } = new() { TypeInfoResolver = Resolver };

    /// <summary>
    /// <see cref="Enum.TryParse{TEnum}(string, bool, out TEnum)"/> first, unchanged, then the
    /// accepted names of the enum's members.
    /// </summary>
    public static bool TryParse<TEnum>(string? name, bool ignoreCase, out TEnum value)
        where TEnum : struct, Enum
    {
        value = default;
        if (string.IsNullOrWhiteSpace(name)) return false;
        return Enum.TryParse(name, ignoreCase, out value) || TryParseAccepted(name, ignoreCase, out value);
    }

    /// <summary>Only the accepted names — for a reader that refuses numbers and parses names itself.</summary>
    public static bool TryParseAccepted<TEnum>(string? name, bool ignoreCase, out TEnum value)
        where TEnum : struct, Enum
    {
        value = default;
        if (string.IsNullOrWhiteSpace(name)) return false;
        var comparison = ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        foreach (var field in typeof(TEnum).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            if (!field.GetCustomAttributes<AcceptedNameAttribute>()
                    .Any(a => string.Equals(a.Name, name, comparison))) continue;
            value = (TEnum)field.GetValue(null)!;
            return true;
        }
        return false;
    }

    private static void AddAliases(JsonTypeInfo typeInfo)
    {
        if (typeInfo.Kind != JsonTypeInfoKind.Object) return;
        foreach (var declared in typeInfo.Properties.ToList())
        {
            if (declared.AttributeProvider is not PropertyInfo member) continue;
            foreach (var alias in member.GetCustomAttributes<AcceptedNameAttribute>())
                typeInfo.Properties.Add(AliasOf(typeInfo, member, alias.Name));
        }
    }

    private static JsonPropertyInfo AliasOf(JsonTypeInfo typeInfo, PropertyInfo member, string name)
    {
        var policy = typeInfo.Options.PropertyNamingPolicy;
        var alias = typeInfo.CreateJsonPropertyInfo(member.PropertyType, policy?.ConvertName(name) ?? name);
        alias.Get = null;
        alias.Set = (target, value) => { if (value is not null) member.SetValue(target, value); };
        return alias;
    }
}
