using System.Text.Json;

namespace AgentSmith.Application.Services.WorkSpecs;

/// <summary>
/// p0390: case-insensitive JSON element access shared by the work-spec parser.
/// Models emit snake_case, camelCase and PascalCase interchangeably; the parser
/// must not turn that into a lost requirement.
/// </summary>
internal static class WorkSpecJsonReader
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
                if (string.Equals(prop.Name.Replace("_", string.Empty), name,
                        StringComparison.OrdinalIgnoreCase))
                {
                    value = prop.Value;
                    return true;
                }
        value = default;
        return false;
    }

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

    public static IEnumerable<JsonElement> ReadObjects(JsonElement obj, string name) =>
        TryGet(obj, name, out var el) && el.ValueKind == JsonValueKind.Array
            ? el.EnumerateArray().Where(e => e.ValueKind == JsonValueKind.Object)
            : [];
}
