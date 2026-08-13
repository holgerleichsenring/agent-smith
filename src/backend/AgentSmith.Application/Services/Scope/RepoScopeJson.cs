using System.Text.Json;

namespace AgentSmith.Application.Services.Scope;

/// <summary>
/// p0413: the case-insensitive JSON lookups the scope-reply readers share, split
/// out of <see cref="RepoScopeParser"/> so the parser owns the reply's SHAPE and
/// these own how one field is read out of it.
/// </summary>
internal static class RepoScopeJson
{
    public static bool TryGet(JsonElement obj, string name, out JsonElement value)
    {
        foreach (var prop in obj.EnumerateObject())
            if (string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = prop.Value;
                return true;
            }
        value = default;
        return false;
    }

    /// <summary>A named string field, or null when absent / not a string.</summary>
    public static string? ReadString(JsonElement obj, string name) =>
        TryGet(obj, name, out var el) && el.ValueKind == JsonValueKind.String
            ? el.GetString()
            : null;
}
