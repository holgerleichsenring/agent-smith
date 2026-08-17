using System.Text.Json;
using AgentSmith.Contracts.Models;

namespace AgentSmith.Infrastructure.Services.Nuclei;

/// <summary>
/// Reads Nuclei's JSONL result stream into findings — the parsing half of the scan, split
/// out from the spawner the way <c>ZapReportParser</c> has always been split from ZAP's.
/// <para>
/// p0429a: a line's <c>request</c>/<c>response</c> pair is kept when Nuclei emitted one,
/// because that pair is the evidence behind a live-target claim. Nuclei only writes it
/// when asked to, and the image is unpinned, so an absent pair is normal — the finding
/// then reaches delivery unrefuted rather than refuted against evidence nobody has.
/// </para>
/// </summary>
internal static class NucleiFindingReader
{
    internal static List<NucleiFinding> ParseJsonLines(string output)
    {
        var findings = new List<NucleiFinding>();

        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                using var doc = JsonDocument.Parse(line);
                var root = doc.RootElement;

                var templateId = Text(root, "template-id") ?? "";
                var info = root.TryGetProperty("info", out var i) ? i : default;
                var name = Text(info, "name") ?? templateId;
                var severity = Text(info, "severity") ?? "info";
                var matchedUrl = Text(root, "matched-at") ?? "";
                var reference = info.ValueKind == JsonValueKind.Object
                    && info.TryGetProperty("reference", out var refArr)
                    && refArr.ValueKind == JsonValueKind.Array
                        ? string.Join(", ", refArr.EnumerateArray().Select(r => r.GetString()))
                        : null;

                findings.Add(new NucleiFinding(
                    templateId, name, severity, matchedUrl, Text(info, "description"), reference,
                    Exchange(root, matchedUrl)));
            }
            catch (JsonException)
            {
                // Skip non-JSON lines (Nuclei status messages)
            }
        }

        return findings;
    }

    private static HttpExchange? Exchange(JsonElement root, string url)
    {
        var request = Text(root, "request");
        var response = Text(root, "response");
        if (request is null && response is null) return null;
        return new HttpExchange(
            Text(root, "type") is "http" or null ? Method(request) : "GET",
            url, Request: request, Response: response);
    }

    /// <summary>Nuclei's raw request starts with its request line: "GET /x HTTP/1.1".</summary>
    private static string Method(string? request) =>
        request?.Split(' ', 2)[0] is { Length: > 0 and < 8 } verb ? verb : "GET";

    private static string? Text(JsonElement element, string property) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(property, out var value)
        && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
