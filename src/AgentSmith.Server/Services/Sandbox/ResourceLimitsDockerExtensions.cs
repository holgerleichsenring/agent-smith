using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Server.Services.Sandbox;

/// <summary>
/// Converts <see cref="ResourceLimits"/> Kubernetes-quantity strings into the
/// integer types Docker's HostConfig expects. Both <c>DockerContainerSpecBuilder</c>
/// (sandbox toolchain) and <c>DockerJobSpawner</c> (orchestrator) parse the same
/// quantities — this extension keeps the parsing in one place.
/// </summary>
internal static class ResourceLimitsDockerExtensions
{
    // Kubernetes CPU quantities: bare number = cores (e.g. "2" = 2 cores),
    // "m" suffix = millicores (e.g. "500m" = 0.5 cores). Docker NanoCpus = 1e9 per core.
    public static long CpuLimitToNanoCpus(this ResourceLimits limits)
        => ParseCpuToNanoCpus(limits.CpuLimit);

    public static long MemoryLimitToBytes(this ResourceLimits limits)
        => ParseMemoryToBytes(limits.MemoryLimit);

    public static long MemoryRequestToBytes(this ResourceLimits limits)
        => ParseMemoryToBytes(limits.MemoryRequest);

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
        var (multiplier, numberPart) = ExtractMemoryMultiplier(trimmed);
        return double.TryParse(numberPart, System.Globalization.CultureInfo.InvariantCulture, out var n)
            ? (long)(n * multiplier) : 0;
    }

    private static (long Multiplier, string NumberPart) ExtractMemoryMultiplier(string trimmed)
    {
        if (trimmed.EndsWith("Gi", StringComparison.OrdinalIgnoreCase))
            return (1024L * 1024L * 1024L, trimmed[..^2]);
        if (trimmed.EndsWith("Mi", StringComparison.OrdinalIgnoreCase))
            return (1024L * 1024L, trimmed[..^2]);
        if (trimmed.EndsWith("Ki", StringComparison.OrdinalIgnoreCase))
            return (1024L, trimmed[..^2]);
        if (trimmed.EndsWith("G", StringComparison.OrdinalIgnoreCase))
            return (1_000_000_000L, trimmed[..^1]);
        if (trimmed.EndsWith("M", StringComparison.OrdinalIgnoreCase))
            return (1_000_000L, trimmed[..^1]);
        if (trimmed.EndsWith("K", StringComparison.OrdinalIgnoreCase))
            return (1_000L, trimmed[..^1]);
        return (1L, trimmed);
    }
}
