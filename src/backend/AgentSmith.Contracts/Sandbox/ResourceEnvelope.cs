namespace AgentSmith.Contracts.Sandbox;

/// <summary>
/// p0336c: the per-field MAX of a set of resource limits — the envelope for ONE
/// sandbox that hosts several same-toolchain contexts of a repo. Collapsing
/// same-image contexts into one pod (they build sequentially under the single
/// agentic loop) means the pod must be sized to the heaviest field of any member.
/// </summary>
public static class ResourceEnvelope
{
    public static ResourceLimits Max(IEnumerable<ResourceLimits> limits)
    {
        var list = limits.ToList();
        if (list.Count == 0) return ResourceLimits.Default;
        if (list.Count == 1) return list[0];
        return new ResourceLimits(
            Largest(list.Select(r => r.CpuRequest), KubernetesQuantity.TryParseCpuToNanoCpus),
            Largest(list.Select(r => r.CpuLimit), KubernetesQuantity.TryParseCpuToNanoCpus),
            Largest(list.Select(r => r.MemoryRequest), KubernetesQuantity.TryParseMemoryToBytes),
            Largest(list.Select(r => r.MemoryLimit), KubernetesQuantity.TryParseMemoryToBytes));
    }

    private delegate bool QuantityParser(string? raw, out long value);

    // Keeps the original quantity STRING of the largest member (e.g. "4Gi"),
    // comparing by parsed magnitude; unparseable values sort as zero.
    private static string Largest(IEnumerable<string> values, QuantityParser parse) =>
        values.OrderByDescending(v => parse(v, out var magnitude) ? magnitude : 0L).First();
}
