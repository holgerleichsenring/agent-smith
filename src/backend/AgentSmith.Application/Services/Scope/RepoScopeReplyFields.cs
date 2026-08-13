using System.Text.Json;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Services.Scope;

/// <summary>
/// p0413: the OPTIONAL fields the scope classifier may add to its reply beside
/// the repo verdicts — the context map, the must-change subset, the complexity
/// tier and the work shape. Split out of <see cref="RepoScopeParser"/>: every
/// one of them is absent-tolerant by design, and each absence has its own
/// documented fallback.
/// </summary>
internal static class RepoScopeReplyFields
{
    // p0336b: optional {"contexts": {"<repo>": ["<ctx>", ...]}} — the per-repo
    // affected-context map. Absent / malformed reads as null so the context
    // evaluator keeps all contexts (conservative), exactly like a missing repos
    // array keeps all repos.
    public static IReadOnlyDictionary<string, IReadOnlyList<string>>? ReadContexts(JsonElement obj)
    {
        if (!RepoScopeJson.TryGet(obj, "contexts", out var el) || el.ValueKind != JsonValueKind.Object)
            return null;
        var map = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var prop in el.EnumerateObject())
        {
            if (prop.Value.ValueKind != JsonValueKind.Array) continue;
            map[prop.Name.Trim()] = Strings(prop.Value);
        }
        return map.Count == 0 ? null : map;
    }

    // p0384: optional {"expected_changes": ["<repo>", ...]} — which of the kept
    // repos must CHANGE (vs kept for inspection). Absent / malformed reads as
    // null so the keystone keeps its anyCode semantics, exactly like a missing
    // repos array keeps all repos. Subset validation happens in the evaluator.
    public static IReadOnlyList<string>? ReadExpectedChanges(JsonElement obj)
    {
        if (!RepoScopeJson.TryGet(obj, "expected_changes", out var el)
            || el.ValueKind != JsonValueKind.Array)
            return null;
        var repos = Strings(el);
        return repos.Count == 0 ? null : repos;
    }

    // p0341c: optional {"complexity": "trivial|small|medium|large"} on the same reply.
    // Absent / unrecognised reads as Unknown so the effective cap falls back to the static
    // per-pipeline default (fail-safe) — the tier only sizes a ceiling, never a gate.
    public static ComplexityTier ReadTier(JsonElement obj) =>
        RepoScopeJson.ReadString(obj, "complexity")?.Trim().ToLowerInvariant() switch
        {
            "trivial" => ComplexityTier.Trivial,
            "small" => ComplexityTier.Small,
            "medium" => ComplexityTier.Medium,
            "large" => ComplexityTier.Large,
            _ => ComplexityTier.Unknown,
        };

    // p0413: optional {"shape": "deterministic|judgement|mixed", "shape_reason": "..."}
    // on the same reply — the SHAPE of the work beside its size. Absent / unrecognised
    // reads as null, and the derivation is then told nothing: it cuts exactly as it did
    // before the shape existed (fail-safe, never a gate).
    public static WorkShapeVerdict? ReadShape(JsonElement obj)
    {
        var shape = RepoScopeJson.ReadString(obj, "shape")?.Trim().ToLowerInvariant() switch
        {
            "deterministic" => WorkShape.Deterministic,
            "judgement" or "judgment" => WorkShape.Judgement,
            "mixed" => WorkShape.Mixed,
            _ => WorkShape.Unknown,
        };
        if (shape == WorkShape.Unknown) return null;
        var reason = RepoScopeJson.ReadString(obj, "shape_reason")?.Trim();
        return new WorkShapeVerdict(shape, string.IsNullOrEmpty(reason) ? null : reason);
    }

    public static string? ReadRationale(JsonElement obj) => RepoScopeJson.ReadString(obj, "rationale");

    private static List<string> Strings(JsonElement array) =>
        [.. array.EnumerateArray()
            .Where(e => e.ValueKind == JsonValueKind.String)
            .Select(e => e.GetString()!.Trim())
            .Where(s => s.Length > 0)];
}
