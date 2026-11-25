using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Server.Services.Sandbox;

/// <summary>
/// 2026-11-25-9992b: one toolchain container of a sandbox pod — its image and limits, and the job id and bus URL of
/// the agent it runs. A single sandbox's pod has one; a run's workspace pod has one per image.
/// </summary>
public sealed record PodToolchain(string Image, ResourceLimits Resources, string JobId, string RedisUrl)
{
    /// <summary>The container name of the toolchain at <paramref name="index"/>.</summary>
    public static string ContainerName(int index) => $"toolchain-{index}";
}
