using System.Globalization;
using System.Text.Json;

namespace AgentSmith.Application.Services.Pocs.Scan;

/// <summary>
/// 2026-11-23-933dc: osv-scanner's JSON report (results[].packages[].groups / vulnerabilities) as one
/// <see cref="PocVulnerability"/> per advisory group and package. The group's id is the ordinal-lowest of its ids, so an
/// overrule survives a database update that reorders the group; the score is the group's max_severity; the fixed
/// versions come from the advisory's affected ranges of that package. Null when the report does not parse.
/// </summary>
public sealed class OsvReportParser
{
    public IReadOnlyList<PocVulnerability>? Parse(string json, string sourcePrefix)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("results", out var results)) return [];
            return [.. results.EnumerateArray().SelectMany(r => FromResult(r, sourcePrefix))];
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException or KeyNotFoundException)
        {
            return null;
        }
    }

    private static IEnumerable<PocVulnerability> FromResult(JsonElement result, string prefix)
    {
        var path = result.GetProperty("source").GetProperty("path").GetString() ?? string.Empty;
        var file = path.StartsWith(prefix, StringComparison.Ordinal) ? path[prefix.Length..] : path;
        if (!result.TryGetProperty("packages", out var packages)) yield break;
        foreach (var package in packages.EnumerateArray())
            foreach (var vulnerability in FromPackage(package, file))
                yield return vulnerability;
    }

    private static IEnumerable<PocVulnerability> FromPackage(JsonElement package, string file)
    {
        var info = package.GetProperty("package");
        var (name, version, ecosystem) = (Text(info, "name"), Text(info, "version"), Text(info, "ecosystem"));
        var advisories = package.TryGetProperty("vulnerabilities", out var v)
            ? v.EnumerateArray().ToDictionary(a => Text(a, "id"), a => a.Clone(), StringComparer.Ordinal)
            : [];
        if (!package.TryGetProperty("groups", out var groups)) yield break;
        foreach (var group in groups.EnumerateArray())
        {
            var ids = group.GetProperty("ids").EnumerateArray().Select(i => i.GetString()!).Order(StringComparer.Ordinal).ToList();
            var members = ids.Where(advisories.ContainsKey).Select(i => advisories[i]).ToList();
            yield return new PocVulnerability(ids[0], name, version, ecosystem, file, Score(group), Fixed(members, name),
                Title(members, ids[0]), Aliases(group, ids));
        }
    }

    private static double? Score(JsonElement group) =>
        group.TryGetProperty("max_severity", out var s) && s.ValueKind == JsonValueKind.String
        && double.TryParse(s.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var score) ? score : null;

    private static IReadOnlyList<string> Fixed(IEnumerable<JsonElement> advisories, string package) =>
        [.. advisories.SelectMany(a => a.TryGetProperty("affected", out var affected) ? affected.EnumerateArray() : [])
            .Where(a => a.TryGetProperty("package", out var p) && string.Equals(Text(p, "name"), package, StringComparison.OrdinalIgnoreCase))
            .SelectMany(a => a.TryGetProperty("ranges", out var ranges) ? ranges.EnumerateArray() : [])
            .SelectMany(r => r.TryGetProperty("events", out var events) ? events.EnumerateArray() : [])
            .Where(e => e.TryGetProperty("fixed", out _)).Select(e => Text(e, "fixed")).Distinct(StringComparer.Ordinal)];

    private static string Title(IReadOnlyList<JsonElement> advisories, string id) =>
        advisories.Select(a => Text(a, "summary")).FirstOrDefault(t => t.Length > 0)
        ?? advisories.Select(a => Text(a, "details").Split('\n')[0]).FirstOrDefault(t => t.Length > 0) ?? id;

    private static IReadOnlyList<string> Aliases(JsonElement group, IReadOnlyList<string> ids) =>
        [.. (group.TryGetProperty("aliases", out var a) ? a.EnumerateArray().Select(x => x.GetString()!) : [])
            .Concat(ids).Where(x => x != ids[0]).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];

    private static string Text(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString()! : string.Empty;
}
