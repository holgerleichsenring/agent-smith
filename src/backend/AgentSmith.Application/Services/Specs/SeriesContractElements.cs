using System.Globalization;
using System.Text.Json;
using AgentSmith.Application.Services.Json;
using AgentSmith.Contracts.Specs;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-10-13-8762j: one <c>contract</c> block read into a <see cref="SeriesContract"/> keyed by spec ids. A member
/// is a 1-based position among <paramref name="ids"/> — an integer, or a string of digits — or a string the
/// source's <paramref name="byId"/> accepts. Every defect is collected, none is guessed around: an unresolvable
/// member, a missing field, a number that is not one, a '*' in a path.
/// </summary>
internal sealed class SeriesContractElements(IReadOnlyList<string> ids, Func<string, string?> byId, string idHint)
{
    public List<string> Problems { get; } = [];

    public SeriesContract Read(JsonElement block) => new(
        [.. Entries(block, "names").Select(e => new SeriesContractName(
            Text(e, "name", "a name"), Text(e, "kind", "a name"), Number(e, "number", "a name"),
            Member(e, "owner", "a name"), Members(e)))],
        [.. Entries(block, "data").Select(e => new SeriesContractDatum(
            Text(e, "datum", "a datum"), Text(e, "lives_in", "a datum"), Member(e, "owner", "a datum"), Members(e)))],
        [.. Entries(block, "areas").Select(e => new SeriesContractArea(
            Member(e, "spec", "an area"), [.. SpecJsonReader.ReadStrings(e, "paths").Select(Path)]))],
        [.. Entries(block, "budgets").Select(e => new SeriesContractBudget(
            Path(Text(e, "path", "a budget")), Member(e, "spec", "a budget"), Number(e, "max_lines", "a budget") ?? 0))]);

    private static IEnumerable<JsonElement> Entries(JsonElement block, string section) =>
        SpecJsonReader.ReadObjects(block, section);

    private string Text(JsonElement entry, string field, string entryName)
    {
        var text = SpecJsonReader.ReadString(entry, field);
        if (text.Length == 0) Problems.Add($"{entryName} entry has no '{field}'");
        return text;
    }

    private int? Number(JsonElement entry, string field, string entryName)
    {
        if (!SpecJsonReader.TryGet(entry, field, out var value) || value.ValueKind == JsonValueKind.Null) return null;
        if (Integer(value) is { } number) return number;
        Problems.Add($"{entryName} entry's '{field}' is not a whole number");
        return null;
    }

    private string Member(JsonElement entry, string field, string entryName)
    {
        if (!SpecJsonReader.TryGet(entry, field, out var value)) { Problems.Add($"{entryName} entry has no '{field}'"); return string.Empty; }
        return Resolve(value);
    }

    private IReadOnlyList<string>? Members(JsonElement entry) =>
        SpecJsonReader.TryGet(entry, "writers", out var list) && list.ValueKind == JsonValueKind.Array
            ? [.. list.EnumerateArray().Select(Resolve)] : null;

    private string Resolve(JsonElement value)
    {
        if (Integer(value) is { } position)
        {
            if (position >= 1 && position <= ids.Count) return ids[position - 1];
            Problems.Add($"position {position} is not a spec of this set of {ids.Count}");
            return position.ToString(CultureInfo.InvariantCulture);
        }
        var text = value.ValueKind == JsonValueKind.String ? value.GetString()!.Trim() : value.GetRawText();
        if (byId(text) is { } id) return id;
        Problems.Add($"member '{text}' — {idHint}");
        return text;
    }

    private string Path(string path)
    {
        if (path.Contains('*')) Problems.Add($"path '{path}' holds a '*' — name the file, or a directory ending in '/'");
        return path.Trim();
    }

    private static int? Integer(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Number => JsonValueReader.Int32(value, int.MinValue) is var n and not int.MinValue ? n : null,
        JsonValueKind.String => int.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : null,
        _ => null,
    };
}
