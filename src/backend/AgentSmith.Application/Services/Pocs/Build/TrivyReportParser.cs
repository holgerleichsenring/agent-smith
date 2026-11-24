using System.Text.Json;

namespace AgentSmith.Application.Services.Pocs.Build;

/// <summary>
/// 2026-11-23-933dd: the build's compact Trivy report — a JSON array, one object per finding at every severity — read
/// into entries. A report that is not that array is a failure in its own words, never a clean pass.
/// </summary>
public sealed class TrivyReportParser
{
    public (IReadOnlyList<TrivyReportEntry> Entries, string? Failure) Parse(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Array) return ([], "the scan report is not a JSON array");
            return ([.. document.RootElement.EnumerateArray().Select(Entry)], null);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or KeyNotFoundException)
        {
            return ([], $"the scan report could not be read: {ex.Message}");
        }
    }

    private static TrivyReportEntry Entry(JsonElement e) => new(
        Text(e, "kind") ?? throw new InvalidOperationException("a finding names no kind"),
        Text(e, "id") ?? throw new InvalidOperationException("a finding names no id"),
        Text(e, "package"), Text(e, "installed"), Empty(Text(e, "fixed")), Text(e, "severity") ?? string.Empty,
        e.TryGetProperty("scores", out var s) && s.ValueKind == JsonValueKind.Array
            ? [.. s.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.Number).Select(x => x.GetDouble())] : [],
        Text(e, "title"), Text(e, "target") ?? string.Empty,
        e.TryGetProperty("line", out var l) && l.ValueKind == JsonValueKind.Number ? l.GetInt32() : null);

    private static string? Text(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static string? Empty(string? text) => string.IsNullOrWhiteSpace(text) ? null : text;
}
