using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Server.Services.Sandbox;

/// <summary>
/// 2026-10-25-c5a7b: the per-job volumes of one Docker sandbox, named after its job slug:
/// shared (the agent binary), work (the source tree) and, for a hardened sandbox, home (/root).
/// Teardown and the orphan remover read <see cref="All"/>, so no volume is left behind.
/// </summary>
public sealed record DockerSandboxVolumeSet(string Shared, string Work, string? Home)
{
    private const string Prefix = "agentsmith-sandbox-";

    public static DockerSandboxVolumeSet For(string slug, SandboxSecurityContext profile) => new(
        $"{Prefix}{slug}-shared", $"{Prefix}{slug}-work", profile.RunAsRoot ? null : $"{Prefix}{slug}-home");

    public IReadOnlyList<string> All => Home is null ? [Shared, Work] : [Shared, Work, Home];
}
