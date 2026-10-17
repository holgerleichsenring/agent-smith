using System.Text.Json;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services;

/// <summary>
/// p0340: the verdict's per-criterion dispositions. 2026-10-17-a196c: each may carry "citations" — the
/// changed paths and the commands its claim rests on — read as a list (a bare string is a list of one) and
/// left null when the entry has no such key, which is how a verdict written before citations reads.
/// </summary>
public static partial class MasterVerificationParser
{
    // p0340: an array of { criterion, status, evidence } objects — one per ratified
    // acceptance criterion, in order. Absent = null (the keystone then treats a run
    // with ratified criteria as unconfirmed). An unrecognised / missing status maps
    // to Unmet by default, so a vague verdict gates RED rather than sliding through.
    private static IReadOnlyList<AcceptanceDisposition>? GetAcceptanceDispositions(
        JsonElement obj, params string[] names)
    {
        foreach (var name in names)
            if (TryGet(obj, name, out var el) && el.ValueKind == JsonValueKind.Array)
                return [.. el.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.Object).Select(Disposition)];
        return null;
    }

    private static AcceptanceDisposition Disposition(JsonElement item) =>
        new(GetString(item, "criterion", "assertion", "text", "expected") ?? "",
            MapAcceptanceStatus(GetString(item, "status", "disposition", "result")),
            GetString(item, "evidence", "reason", "why", "note") ?? "")
        {
            Citations = GetCitations(item),
        };

    private static IReadOnlyList<string>? GetCitations(JsonElement item)
    {
        if (!TryGet(item, "citations", out var el)) return null;
        return el.ValueKind switch
        {
            JsonValueKind.Array => [.. el.EnumerateArray()
                .Where(c => c.ValueKind == JsonValueKind.String)
                .Select(c => c.GetString()!)
                .Where(c => !string.IsNullOrWhiteSpace(c))],
            JsonValueKind.String when !string.IsNullOrWhiteSpace(el.GetString()) => [el.GetString()!],
            _ => [],
        };
    }

    private static AcceptanceStatus MapAcceptanceStatus(string? raw) => raw?.Trim().ToLowerInvariant() switch
    {
        "met" or "satisfied" or "done" or "pass" or "passed" or "yes" => AcceptanceStatus.Met,
        "not_applicable" or "not-applicable" or "n/a" or "na" or "moot" or "vacuous"
            => AcceptanceStatus.NotApplicable,
        _ => AcceptanceStatus.Unmet,
    };
}
