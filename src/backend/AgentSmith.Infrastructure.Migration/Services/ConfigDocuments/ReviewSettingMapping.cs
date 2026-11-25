using System.Text.Json.Nodes;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Infrastructure.Migration.Services.ConfigDocuments;

/// <summary>
/// 2026-11-25-9992i: the retired merge keys of one stored merge block as the review they map to, erring toward more human
/// review: series_when manual is each; else when manual or absent is end; else (on_ci_green, on_gate_green) none. And the
/// ways the project's standalone tickets now behave otherwise, for the migration's log.
/// </summary>
public static class ReviewSettingMapping
{
    /// <summary>The four keys the migration drops, as the stored document spells them.</summary>
    public static IReadOnlyList<string> RetiredKeys { get; } = ["When", "SeriesWhen", "RespectBranchProtection", "RefreshOpenPullRequests"];

    // The stored MergeWhen numbers (manual 0, on_gate_green 1, on_ci_green 2) and their YAML and member names.
    private const int Manual = 0; // not a limit: the stored number of manual
    private const int OnGateGreen = 1; // not a limit: the stored number of on_gate_green
    private const int OnCiGreen = 2; // not a limit: the stored number of on_ci_green

    public static ReviewMode Of(JsonObject merge)
    {
        ArgumentNullException.ThrowIfNull(merge);
        if (Mode(Value(merge, "SeriesWhen")) == Manual) return ReviewMode.Each;
        return Mode(Value(merge, "When")) is null or Manual ? ReviewMode.End : ReviewMode.None;
    }

    /// <summary>How the project's standalone tickets now behave otherwise; empty when they behave as before.</summary>
    public static IReadOnlyList<string> Changes(JsonObject merge, ReviewMode review)
    {
        ArgumentNullException.ThrowIfNull(merge);
        var when = Mode(Value(merge, "When")) ?? Manual;
        var changes = new List<string>();
        if (review == ReviewMode.Each && when != Manual)
            changes.Add("standalone tickets now wait for a person instead of merging by themselves");
        if (review == ReviewMode.None && when == OnGateGreen)
            changes.Add("standalone tickets now merge once their CI is green, no longer at the end of their run");
        if (review == ReviewMode.None && !On(Value(merge, "RespectBranchProtection")))
            changes.Add("a merge on green now arms the host's auto-merge instead of bypassing its branch protection; on GitHub the "
                + "repository needs allow_auto_merge turned on, or the merge is refused");
        return changes;
    }

    /// <summary>Whether the block names any retired key.</summary>
    public static bool Names(JsonObject merge) => merge.Any(p => RetiredKeys.Any(k => Same(k, p.Key)));

    /// <summary>The block without the retired keys.</summary>
    public static void Drop(JsonObject merge)
    {
        foreach (var key in merge.Select(p => p.Key).Where(k => RetiredKeys.Any(r => Same(r, k))).ToList()) merge.Remove(key);
    }

    private static bool On(JsonNode? value) => value is JsonValue v && v.TryGetValue<bool>(out var on) && on;

    private static int? Mode(JsonNode? value) => value switch
    {
        JsonValue v when v.TryGetValue<int>(out var number) => number,
        JsonValue v when v.TryGetValue<string>(out var name) => Name(name),
        _ => null,
    };

    private static int? Name(string name) => name.Replace("_", string.Empty).Trim().ToLowerInvariant() switch
    {
        "manual" => Manual,
        "ongategreen" => OnGateGreen,
        "oncigreen" => OnCiGreen,
        _ => null,
    };

    private static JsonNode? Value(JsonObject merge, string key) => merge.FirstOrDefault(p => Same(p.Key, key)).Value;

    // The store reads property names case-insensitively, so any spelling of a key is that key.
    private static bool Same(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
}
