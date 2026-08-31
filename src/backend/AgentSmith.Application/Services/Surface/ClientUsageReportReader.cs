using System.Text.Json;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Surface;

/// <summary>
/// 2026-08-30-c6ec: reads the call-site report out of whatever the model wrote around it.
/// <para>
/// Tolerant about framing, strict about substance: an answer with no readable object is
/// NOT "this client calls nothing" — it is a failed reading, and the caller must be able
/// to tell those apart, because the first would report the whole interface as unexercised.
/// </para>
/// </summary>
public static class ClientUsageReportReader
{
    public static ReportedClientUsage? Read(string? text)
    {
        var document = Parse(text);
        if (document is null) return null;
        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return null;
            return new ReportedClientUsage(CallSites(root), Undecided(root));
        }
    }

    private static JsonDocument? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        if (start < 0 || end <= start) return null;
        try
        {
            return JsonDocument.Parse(text[start..(end + 1)]);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static IReadOnlyList<ClientCallSite> CallSites(JsonElement root) =>
    [
        .. Array(root, "call_sites")
            .Select(site => new ClientCallSite(
                Text(site, "file") ?? string.Empty,
                Text(site, "operation") ?? string.Empty,
                Strings(site, "sends"),
                Strings(site, "reads")))
            .Where(site => site.Operation.Length > 0),
    ];

    private static IReadOnlyList<UndecidedClientFile> Undecided(JsonElement root) =>
    [
        .. Array(root, "undecided")
            .Select(file => new UndecidedClientFile(
                Text(file, "file") ?? string.Empty, Text(file, "why") ?? string.Empty))
            .Where(file => file.File.Length > 0),
    ];

    private static IEnumerable<JsonElement> Array(JsonElement root, string name) =>
        root.TryGetProperty(name, out var array) && array.ValueKind == JsonValueKind.Array
            ? array.EnumerateArray().Where(e => e.ValueKind == JsonValueKind.Object)
            : [];

    private static IReadOnlyList<string> Strings(JsonElement element, string name) =>
    [
        .. element.TryGetProperty(name, out var array) && array.ValueKind == JsonValueKind.Array
            ? array.EnumerateArray()
                .Where(e => e.ValueKind == JsonValueKind.String)
                .Select(e => e.GetString()!)
            : [],
    ];

    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
