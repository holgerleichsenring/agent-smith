using System.Text.Json;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Services.Scope;

/// <summary>
/// p0413: the fields of the scope call's reply — the complexity tier, the work shape and the refusal
/// (2026-11-19-6b0bb: the repo verdicts, contexts and expected changes are no longer asked for). Every one
/// is absent-tolerant by design, and each absence has its own documented fallback.
/// </summary>
internal static class RepoScopeReplyFields
{
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
    // reads as null: nothing is recorded on the run row (fail-safe, never a gate).
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

    // Optional {"refusal": null | {"quote": "...", "reason": "..."}} — the security
    // judgement over the ticket. Absent, null or malformed reads as null: no objection,
    // and the run proceeds exactly as it did before the field existed. An object that
    // states neither a quote nor a reason is not a refusal either — a parked run must
    // be able to say WHAT was refused.
    public static ScopeRefusal? ReadRefusal(JsonElement obj)
    {
        if (!RepoScopeJson.TryGet(obj, "refusal", out var el) || el.ValueKind != JsonValueKind.Object)
            return null;
        var quote = RepoScopeJson.ReadString(el, "quote")?.Trim() ?? string.Empty;
        var reason = RepoScopeJson.ReadString(el, "reason")?.Trim() ?? string.Empty;
        return quote.Length == 0 && reason.Length == 0 ? null : new ScopeRefusal(quote, reason);
    }
}
