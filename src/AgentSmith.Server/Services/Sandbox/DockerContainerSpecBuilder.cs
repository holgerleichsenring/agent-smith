using AgentSmith.Contracts.Sandbox;
using Docker.DotNet.Models;

namespace AgentSmith.Server.Services.Sandbox;

/// <summary>
/// Builds the CreateContainerParameters pair for the sandbox: an agent-loader
/// initContainer that copies /agent into a shared volume, and a toolchain main
/// container that runs /shared/agent against a separate /work volume.
/// </summary>
public sealed class DockerContainerSpecBuilder
{
    public const string SharedMount = "/shared";
    public const string WorkMount = "/work";

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

    public CreateContainerParameters BuildToolchain(
        string containerName,
        string sharedVolume,
        string workVolume,
        string jobId,
        string redisUrl,
        SandboxSpec spec) => new()
    {
        Name = containerName,
        Image = spec.ToolchainImage,
        Cmd = [$"{SharedMount}/agent", "--redis-url", redisUrl, "--job-id", jobId],
        WorkingDir = WorkMount,
        Env = BuildEnv(jobId, redisUrl),
        HostConfig = BuildHostConfig(sharedVolume, workVolume, spec)
    };

    private static List<string> BuildEnv(string jobId, string redisUrl) =>
    [
        $"JOB_ID={jobId}",
        $"REDIS_URL={redisUrl}"
    ];

    private static HostConfig BuildHostConfig(string sharedVolume, string workVolume, SandboxSpec spec)
    {
        var r = spec.Resources;
        return new HostConfig
        {
            AutoRemove = false,
            Binds =
            [
                $"{sharedVolume}:{SharedMount}:ro",
                $"{workVolume}:{WorkMount}"
            ],
            NanoCPUs = ParseCpuToNanoCpus(r.CpuLimit),
            Memory = ParseMemoryToBytes(r.MemoryLimit),
            MemoryReservation = ParseMemoryToBytes(r.MemoryRequest)
        };
    }

    // Kubernetes CPU quantities: bare number = cores (e.g. "2" = 2 cores),
    // "m" suffix = millicores (e.g. "500m" = 0.5 cores). Docker NanoCpus = 1e9 per core.
    private static long ParseCpuToNanoCpus(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return 0;
        var trimmed = raw.Trim();
        if (trimmed.EndsWith("m", StringComparison.Ordinal))
        {
            return long.TryParse(trimmed[..^1], out var milli)
                ? milli * 1_000_000L : 0;
        }
        return double.TryParse(trimmed, System.Globalization.CultureInfo.InvariantCulture, out var cores)
            ? (long)(cores * 1_000_000_000L) : 0;
    }

    private static long ParseMemoryToBytes(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return 0;
        var trimmed = raw.Trim();
        var multiplier = 1L;
        var numberPart = trimmed;
        if (trimmed.EndsWith("Gi", StringComparison.OrdinalIgnoreCase))
        {
            multiplier = 1024L * 1024L * 1024L;
            numberPart = trimmed[..^2];
        }
        else if (trimmed.EndsWith("Mi", StringComparison.OrdinalIgnoreCase))
        {
            multiplier = 1024L * 1024L;
            numberPart = trimmed[..^2];
        }
        else if (trimmed.EndsWith("G", StringComparison.OrdinalIgnoreCase))
        {
            multiplier = 1_000_000_000L;
            numberPart = trimmed[..^1];
        }
        else if (trimmed.EndsWith("M", StringComparison.OrdinalIgnoreCase))
        {
            multiplier = 1_000_000L;
            numberPart = trimmed[..^1];
        }
        return double.TryParse(numberPart, System.Globalization.CultureInfo.InvariantCulture, out var n)
            ? (long)(n * multiplier) : 0;
    }
}
