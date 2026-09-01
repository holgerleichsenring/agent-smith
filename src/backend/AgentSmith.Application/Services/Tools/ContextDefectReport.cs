using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// 2026-08-26-167c: words every defect a context document carries into ONE rejection.
/// <para>
/// Three defects at a time cannot converge inside a five-refusal budget: a realistic
/// off-vocabulary document carries about twenty, six of them on <c>arch.patterns</c>
/// alone. And a 400-character cap on a quoted rule silently DROPPED that field's 36
/// values — the guessing the rejection exists to end, still live.
/// </para>
/// <para>
/// So: defects that broke the same rule are grouped and the rule is quoted once with
/// the offending locations beside it, a long list is truncated to a head plus a count
/// rather than dropped, and the whole message is bounded by characters.
/// </para>
/// </summary>
public sealed class ContextDefectReport(ContextSchemaPointer pointer)
{
    private const int MaxReportLength = 4000;
    private const int MaxLocationsPerRule = 6;
    private const int MaxSuggestions = 8;
    private const int MaxRuleLength = 300;
    private const int MaxDescriptionLength = 220;

    /// <summary>
    /// The one rejection, or null when nothing was wrong. <paramref name="leadingDefect"/>
    /// is the stack-image rule's — reported ALONGSIDE the schema's, never instead of it.
    /// </summary>
    public string? Compose(string? leadingDefect, IReadOnlyList<ContextSchemaDefect> defects)
    {
        ArgumentNullException.ThrowIfNull(defects);
        var lines = new List<string>();
        if (!string.IsNullOrWhiteSpace(leadingDefect)) lines.Add(leadingDefect);
        lines.AddRange(Grouped(defects));
        return lines.Count == 0 ? null : Bounded(lines);
    }

    private IEnumerable<string> Grouped(IReadOnlyList<ContextSchemaDefect> defects) =>
        defects
            .DistinctBy(defect => (defect.Location, defect.Keyword))
            .GroupBy(defect => (defect.SchemaPath, defect.Keyword, defect.Message))
            .Select(group => Line([.. group]))
            .OrderBy(line => line, StringComparer.Ordinal);

    private string Line(IReadOnlyList<ContextSchemaDefect> group)
    {
        var locations = Locations(group.Select(defect => defect.Location));
        var rule = Rule(group[0]);
        return rule is null
            ? $"{locations}: {group[0].Message}"
            : $"{locations}: {group[0].Message} ({rule})";
    }

    private static string Locations(IEnumerable<string> raw)
    {
        var all = raw.Select(location => location.Length == 0 ? "/" : location)
            .Distinct(StringComparer.Ordinal).ToList();
        var head = string.Join(", ", all.Take(MaxLocationsPerRule));
        return all.Count <= MaxLocationsPerRule
            ? head
            : $"{head} and {all.Count - MaxLocationsPerRule} more";
    }

    // The broken rule, its suggestions and what the field is for — read back out of the
    // schema document, because a keyword the model cannot look up invites guessing. A
    // BOOLEAN node — a closed object's `false` — carries none of that, and threw as one.
    private string? Rule(ContextSchemaDefect defect)
    {
        if (pointer.Resolve(defect.SchemaPath) is not JsonObject node) return null;
        var parts = new List<string>();
        if (node[defect.Keyword] is { } broken) parts.Add($"schema {defect.Keyword}: {Render(broken)}");
        if (node["examples"] is JsonArray examples) parts.Add($"suggestions: {RenderList(examples)}");
        if (node["description"] is JsonValue description
            && description.TryGetValue<string>(out var text))
            parts.Add(Truncate(text, MaxDescriptionLength));
        return parts.Count == 0 ? null : string.Join("; ", parts);
    }

    private static string Render(JsonNode value) =>
        value is JsonArray array
            ? RenderList(array)
            : Truncate(value.ToJsonString(Readable), MaxRuleLength);

    private static string RenderList(JsonArray array)
    {
        var head = string.Join(", ",
            array.Take(MaxSuggestions).Select(item => item?.ToJsonString(Readable) ?? "null"));
        return array.Count <= MaxSuggestions ? head : $"{head} and {array.Count - MaxSuggestions} more";
    }

    private static string Bounded(IReadOnlyList<string> lines)
    {
        var report = new StringBuilder();
        var shown = 0;
        foreach (var line in lines)
        {
            var candidate = shown == 0 ? line : "\n" + line;
            if (report.Length + candidate.Length > MaxReportLength) break;
            report.Append(candidate);
            shown++;
        }
        if (shown == lines.Count) return report.ToString();
        if (shown == 0) report.Append(Truncate(lines[0], MaxReportLength - 80));
        return report.Append($"\n… and {lines.Count - Math.Max(shown, 1)} further defect(s) not shown.")
            .ToString();
    }

    // The default encoder escapes '+' as \u002B, which turns a quoted regex into
    // something the model has to decode before it can act on it.
    private static readonly JsonSerializerOptions Readable =
        new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    private static string Truncate(string text, int max) =>
        text.Length <= max ? text : string.Concat(text.AsSpan(0, max), "…");
}
