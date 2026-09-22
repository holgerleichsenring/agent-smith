using AgentSmith.Contracts.Sandbox;
using Docker.DotNet.Models;

namespace AgentSmith.Server.Services.Sandbox;

/// <summary>
/// Builds the CreateContainerParameters pair for the sandbox: an agent-loader
/// initContainer that copies /agent into a shared volume, and a toolchain main
/// container that runs /shared/agent against a separate /work volume.
/// </summary>
public sealed class DockerContainerSpecBuilder(SandboxOwnerIdentity owner)
{
    public const string SharedMount = "/shared";
    public const string WorkMount = "/work";

    /// <summary>p0201: stable docker label keys for ownership + run scoping. The
    /// orphan reaper filters on JobIdLabel; the watcher and reaper read RunIdLabel
    /// to scope cancel + cleanup to one run. p0465: OwnerLabel names the liveness
    /// store this sandbox belongs to — the reaper and the capacity probe both filter
    /// on it, so a container of another store is neither reaped nor counted.</summary>
    public const string JobIdLabel = "agent-smith.job-id";
    public const string RunIdLabel = "agent-smith.run-id";
    public const string OwnerLabel = "agent-smith.owner";

    /// <summary>2026-09-22-2d11a: the design conversation a source-scope sandbox belongs
    /// to. The orphan reaper reads it to tell a held sandbox from a corpse.</summary>
    public const string ConversationIdLabel = "agent-smith.conversation-id";

    public CreateContainerParameters BuildLoader(string containerName, string sharedVolume, string agentImage) => new()
    {
        Name = containerName,
        Image = agentImage,
        Cmd = ["--inject", $"{SharedMount}/agent"],
        HostConfig = new HostConfig
        {
            AutoRemove = false,
            Binds = [$"{sharedVolume}:{SharedMount}"]
        }
    };

    /// <param name="packageCaches">
    /// p0407: persistent caches to mount and point the toolchain at. Empty (the default)
    /// keeps the historic container shape — no extra bind, no extra env.
    /// </param>
    public CreateContainerParameters BuildToolchain(
        string containerName,
        string sharedVolume,
        string workVolume,
        string jobId,
        string redisUrl,
        SandboxSpec spec,
        IReadOnlyList<PackageCacheVolume>? packageCaches = null) => new()
    {
        Name = containerName,
        Image = spec.ToolchainImage,
        // p0360b: --run-id arms the agent's run-alive idle guard (see PodSpecBuilder).
        Cmd = string.IsNullOrEmpty(spec.RunId)
            ? [$"{SharedMount}/agent", "--redis-url", redisUrl, "--job-id", jobId]
            : [$"{SharedMount}/agent", "--redis-url", redisUrl, "--job-id", jobId, "--run-id", spec.RunId],
        WorkingDir = WorkMount,
        Env = BuildEnv(jobId, redisUrl, packageCaches),
        Labels = BuildLabels(jobId, spec.RunId, spec.ConversationId),
        HostConfig = BuildHostConfig(sharedVolume, workVolume, spec, packageCaches)
    };

    private Dictionary<string, string> BuildLabels(string jobId, string? runId, string? conversationId)
    {
        var labels = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [JobIdLabel] = jobId,
            [OwnerLabel] = owner.Value
        };
        if (!string.IsNullOrEmpty(runId)) labels[RunIdLabel] = runId;
        if (!string.IsNullOrEmpty(conversationId)) labels[ConversationIdLabel] = conversationId;
        return labels;
    }

    private static List<string> BuildEnv(
        string jobId, string redisUrl, IReadOnlyList<PackageCacheVolume>? packageCaches) =>
    [
        $"JOB_ID={jobId}",
        $"REDIS_URL={redisUrl}",
        // p0407: the cache env vars (NUGET_PACKAGES, …) are inherited by every command
        // the agent spawns, which is what makes a restore land in the cache volume.
        .. (packageCaches ?? []).SelectMany(c => c.EnvAssignments)
    ];

    private static HostConfig BuildHostConfig(
        string sharedVolume, string workVolume, SandboxSpec spec,
        IReadOnlyList<PackageCacheVolume>? packageCaches)
    {
        var r = spec.Resources;
        var binds = new List<string>
        {
            $"{sharedVolume}:{SharedMount}:ro",
            $"{workVolume}:{WorkMount}"
        };
        binds.AddRange((packageCaches ?? []).Select(c => c.Bind));
        if (spec.ExtraBinds is { Count: > 0 })
            binds.AddRange(spec.ExtraBinds);
        return new HostConfig
        {
            AutoRemove = false,
            Binds = binds,
            NanoCPUs = r.CpuLimitToNanoCpus(),
            Memory = r.MemoryLimitToBytes(),
            MemoryReservation = r.MemoryRequestToBytes()
        };
    }
}
