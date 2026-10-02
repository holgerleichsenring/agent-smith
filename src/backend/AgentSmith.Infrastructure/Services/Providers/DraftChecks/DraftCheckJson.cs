using System.Text.Json;

namespace AgentSmith.Infrastructure.Services.Providers.DraftChecks;

/// <summary>2026-10-02-5f89b: the few JSON reads the draft checks make, tolerant of a missing field.</summary>
public static class DraftCheckJson
{
    public const int PageSize = 100;

    public static string? Text(this JsonElement json, string property) =>
        json.ValueKind == JsonValueKind.Object
        && json.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    /// <summary>The length of a top-level array page; a full page means more may exist.</summary>
    public static (int Count, bool More)? Page(this JsonElement json) =>
        json.ValueKind == JsonValueKind.Array ? (json.GetArrayLength(), json.GetArrayLength() >= PageSize) : null;

    /// <summary>The length of the array under <paramref name="property"/>; a full page means more may exist.</summary>
    public static (int Count, bool More)? PageUnder(this JsonElement json, string property) =>
        json.ValueKind == JsonValueKind.Object
        && json.TryGetProperty(property, out var items) && items.ValueKind == JsonValueKind.Array
            ? (items.GetArrayLength(), items.GetArrayLength() >= PageSize)
            : null;
}
