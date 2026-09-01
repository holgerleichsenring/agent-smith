using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// 2026-08-28-b630: the one reading of a declared <c>secretName:key</c> reference. The
/// resolver builds the pod spec from it and the preflight check reports on it, so a
/// reference the gate calls well-formed is exactly one the pod spec can carry — two
/// readings would eventually disagree about which typo is a typo.
/// </summary>
public static class SandboxSecretReference
{
    /// <summary>True when <paramref name="value"/> is a secret name and a key joined by
    /// one separator, both sides non-empty. Nothing else is guessed at.</summary>
    public static bool TryParse(string? value, out SecretRef? reference)
    {
        reference = null;
        var parts = (value ?? string.Empty).Split(':', 2);
        if (parts.Length != 2 || parts[0].Length == 0 || parts[1].Length == 0) return false;
        reference = new SecretRef(parts[0], parts[1]);
        return true;
    }
}
