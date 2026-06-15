using System.Globalization;

namespace AgentSmith.Contracts.Sandbox;

/// <summary>
/// Parses Kubernetes-quantity strings (e.g. "250m", "2", "512Mi", "4Gi") into the
/// integer types Docker's HostConfig expects. p0268: lifted out of the Server-layer
/// <c>ResourceLimitsDockerExtensions</c> so the Application-layer sandbox resource
/// resolver can VALIDATE LLM-authored context.yaml quantities with the exact same
/// parse the spawner uses to size containers — one parser, both layers, no second
/// regex/allow-list. <c>TryParse…</c> returns false on malformed input so the
/// resolver can reject a bad block and fall back loudly; the Docker extension keeps
/// its historic "0 on failure" behavior by mapping false → 0.
/// </summary>
public static class KubernetesQuantity
{
    // Kubernetes CPU quantities: bare number = cores (e.g. "2" = 2 cores),
    // "m" suffix = millicores (e.g. "500m" = 0.5 cores). Docker NanoCpus = 1e9 per core.
    public static bool TryParseCpuToNanoCpus(string? raw, out long nanoCpus)
    {
        nanoCpus = 0;
        if (string.IsNullOrEmpty(raw)) return false;
        var trimmed = raw.Trim();
        if (trimmed.EndsWith("m", StringComparison.Ordinal))
        {
            if (!long.TryParse(trimmed[..^1], out var milli)) return false;
            nanoCpus = milli * 1_000_000L;
            return nanoCpus > 0;
        }
        if (!double.TryParse(trimmed, CultureInfo.InvariantCulture, out var cores)) return false;
        nanoCpus = (long)(cores * 1_000_000_000L);
        return nanoCpus > 0;
    }

    public static bool TryParseMemoryToBytes(string? raw, out long bytes)
    {
        bytes = 0;
        if (string.IsNullOrEmpty(raw)) return false;
        var trimmed = raw.Trim();
        var (multiplier, numberPart) = ExtractMemoryMultiplier(trimmed);
        if (!double.TryParse(numberPart, CultureInfo.InvariantCulture, out var n)) return false;
        bytes = (long)(n * multiplier);
        return bytes > 0;
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
