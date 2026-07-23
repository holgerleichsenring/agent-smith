using System.Text.Json;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services;

/// <summary>
/// p0365: extracts an OPTIONAL blocked claim — { "blocked": true, "blocker": "..." } —
/// from the master's final answer, reusing the same tolerant block scan as the verdict
/// parser. Orthogonal to <see cref="MasterVerification"/>: absent → null (no claim), so
/// no new VerificationStatus and no keystone change. The re-engagement driver honours it
/// only when the blocker is concrete (<see cref="ReengageProgressPolicy.ShouldRespectBlock"/>).
/// </summary>
public static partial class MasterVerificationParser
{
    public static MasterBlockedClaim? TryParseBlockedClaim(string? finalText)
    {
        if (string.IsNullOrWhiteSpace(finalText)) return null;
        foreach (var json in CandidateBlocks(finalText))
            if (TryReadBlockedClaim(json, out var claim))
                return claim;
        return null;
    }

    private static bool TryReadBlockedClaim(string json, out MasterBlockedClaim claim)
    {
        claim = null!;
        JsonDocument doc;
        try { doc = JsonDocument.Parse(json); }
        catch (JsonException) { return false; }
        using (doc)
        {
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return false;
            if (!TryGetBool(doc.RootElement, out var blocked, "blocked", "is_blocked", "isBlocked"))
                return false;
            claim = new MasterBlockedClaim(blocked, GetString(doc.RootElement, "blocker", "blocker_reason", "reason"));
            return true;
        }
    }

    private static bool TryGetBool(JsonElement obj, out bool value, params string[] names)
    {
        foreach (var name in names)
            if (TryGet(obj, name, out var el) && el.ValueKind is JsonValueKind.True or JsonValueKind.False)
            {
                value = el.GetBoolean();
                return true;
            }
        value = false;
        return false;
    }
}
