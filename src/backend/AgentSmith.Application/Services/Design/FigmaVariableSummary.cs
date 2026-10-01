using System.Text;
using System.Text.Json;

namespace AgentSmith.Application.Services.Design;

/// <summary>
/// 2026-10-01-7f7ab: a Figma local-variables response as names and values per mode, grouped by
/// collection, under a character budget with truncation stated. An alias shows the name of the
/// variable it points at. Only what the response holds is rendered — no token is guessed.
/// </summary>
public static class FigmaVariableSummary
{
    /// <summary>Variable id to variable name, for resolving a node's bound variables.</summary>
    public static IReadOnlyDictionary<string, string> Names(JsonElement variablesResponse) =>
        FigmaJson.Members(FigmaJson.Obj(Meta(variablesResponse), "variables"))
            .Select(v => (Id: v.Name, Label: FigmaJson.Str(v.Value, "name")))
            .Where(v => v.Label is not null)
            .ToDictionary(v => v.Id, v => v.Label!, StringComparer.Ordinal);

    /// <summary>Renders every collection's variables with their value in each mode.</summary>
    public static string Render(JsonElement variablesResponse, int budget)
    {
        var meta = Meta(variablesResponse);
        var variables = FigmaJson.Members(FigmaJson.Obj(meta, "variables")).ToList();
        var text = new StringBuilder().AppendLine($"variables: {variables.Count}");
        var truncated = false;
        foreach (var line in Lines(meta, variables, Names(variablesResponse)))
        {
            if (text.Length + line.Length + 1 > budget) { truncated = true; break; }
            text.AppendLine(line);
        }
        if (truncated)
            text.AppendLine($"[truncated: variables beyond the {budget}-character budget are not shown]");
        return text.ToString().TrimEnd();
    }

    private static IEnumerable<string> Lines(
        JsonElement meta, List<JsonProperty> variables, IReadOnlyDictionary<string, string> names)
    {
        foreach (var collection in FigmaJson.Members(FigmaJson.Obj(meta, "variableCollections")))
        {
            var modes = FigmaJson.Arr(collection.Value, "modes")
                .Select(m => (Id: FigmaJson.Str(m, "modeId") ?? "", Name: FigmaJson.Str(m, "name") ?? "?")).ToList();
            yield return $"collection \"{FigmaJson.Str(collection.Value, "name")}\""
                + $" (modes: {string.Join(", ", modes.Select(m => m.Name))})";
            foreach (var v in variables.Where(v => FigmaJson.Str(v.Value, "variableCollectionId") == collection.Name))
                yield return $"  {FigmaJson.Str(v.Value, "name")}: " + string.Join(" · ", modes.Select(m =>
                    $"{m.Name} {Value(FigmaJson.Obj(v.Value, "valuesByMode"), m.Id, names)}"));
        }
    }

    private static JsonElement Meta(JsonElement response) =>
        FigmaJson.Obj(response, "meta") ?? default;

    private static string Value(JsonElement? valuesByMode, string modeId, IReadOnlyDictionary<string, string> names)
    {
        if (valuesByMode is not { } values || !values.TryGetProperty(modeId, out var value)) return "-";
        return value.ValueKind switch
        {
            JsonValueKind.Number => FigmaJson.Short(value.GetDouble()),
            JsonValueKind.String => $"\"{value.GetString()}\"",
            JsonValueKind.True or JsonValueKind.False => value.GetRawText(),
            JsonValueKind.Object when FigmaJson.Str(value, "type") == "VARIABLE_ALIAS" =>
                $"-> {(FigmaJson.Str(value, "id") is { } id && names.TryGetValue(id, out var n) ? n : FigmaJson.Str(value, "id"))}",
            JsonValueKind.Object => FigmaJson.Hex(value),
            _ => "-",
        };
    }
}
