namespace AgentSmith.Application.Models.Registry;

/// <summary>
/// Outcome of the <c>RegistryAuthPathGuard</c> allowlist check. When allowed,
/// <see cref="NormalizedPath"/> is the absolute sandbox path to write
/// (<c>~/</c> resolved to the sandbox home). When rejected, <see cref="Reason"/>
/// names why — surfaced on the run's decisions channel, never silent.
/// </summary>
public sealed record RegistryAuthPathVerdict(
    bool IsAllowed, string? NormalizedPath, string? Reason)
{
    public static RegistryAuthPathVerdict Allow(string normalizedPath) =>
        new(true, normalizedPath, null);
    public static RegistryAuthPathVerdict Reject(string reason) =>
        new(false, null, reason);
}
