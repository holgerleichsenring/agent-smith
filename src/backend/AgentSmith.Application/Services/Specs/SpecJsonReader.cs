using System.Text.Json;
using AgentSmith.Application.Services.Json;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// p0393a: case-insensitive JSON element access shared by the derivation parser.
/// Models emit snake_case, camelCase and PascalCase interchangeably; the parser
/// must not turn that into a lost phase.
/// </summary>
internal static class SpecJsonReader
{
    public static IEnumerable<string> BalancedObjects(string text)
    {
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] != '{') continue;
            var depth = 0;
            for (var j = i; j < text.Length; j++)
            {
                if (text[j] == '{') depth++;
                else if (text[j] == '}' && --depth == 0)
                {
                    yield return text[i..(j + 1)];
                    i = j;
                    break;
                }
            }
        }
    }

    public static bool TryGet(JsonElement obj, string name, out JsonElement value)
    {
        if (obj.ValueKind == JsonValueKind.Object)
            foreach (var prop in obj.EnumerateObject())
                if (string.Equals(Normalize(prop.Name), Normalize(name),
                        StringComparison.OrdinalIgnoreCase))
                {
                    value = prop.Value;
                    return true;
                }
        value = default;
        return false;
    }

    // p0400b: BOTH sides are normalised. Folding only the model's side made the
    // lookup name a hidden format — a call site asking for the field as the prompt
    // and the schema spell it ("ships_code") silently matched nothing and every
    // reader fell back to its default.
    private static string Normalize(string name) =>
        name.Replace("_", string.Empty).Replace("-", string.Empty);

    public static string ReadString(JsonElement obj, string name) =>
        TryGet(obj, name, out var el) && el.ValueKind == JsonValueKind.String
            ? el.GetString()!.Trim() : string.Empty;

    public static IReadOnlyList<string> ReadStrings(JsonElement obj, string name) =>
        TryGet(obj, name, out var el) && el.ValueKind == JsonValueKind.Array
            ? [.. el.EnumerateArray()
                .Where(e => e.ValueKind == JsonValueKind.String)
                .Select(e => e.GetString()!.Trim())
                .Where(s => s.Length > 0)]
            : [];

    public static IReadOnlyList<int> ReadInts(JsonElement obj, string name) =>
        TryGet(obj, name, out var el) && el.ValueKind == JsonValueKind.Array
            ? [.. el.EnumerateArray()
                .Select(e => JsonValueReader.Int32(e, -1))
                .Where(v => v > 0)]
            : [];

    public static IEnumerable<JsonElement> ReadObjects(JsonElement obj, string name) =>
        TryGet(obj, name, out var el) && el.ValueKind == JsonValueKind.Array
            ? el.EnumerateArray().Where(e => e.ValueKind == JsonValueKind.Object)
            : [];

    public static int ReadInt(JsonElement obj, string name) => JsonValueReader.Int32(obj, name);

    public static bool ReadBool(JsonElement obj, string name, bool fallback) =>
        TryReadBool(obj, name, out var value) ? value : fallback;

    /// <summary>
    /// p0400c: distinguishes DECLARED from ABSENT. A caller that must not default —
    /// because the prompt made the field an obligation — cannot use a fallback: it
    /// has to see that the model said nothing.
    /// </summary>
    public static bool TryReadBool(JsonElement obj, string name, out bool value)
    {
        if (TryGet(obj, name, out var el)
            && el.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            value = JsonValueReader.Bool(obj, name);
            return true;
        }
        value = false;
        return false;
    }
}
