namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// 2026-11-23-c81eb: the problems of a PoC target's quota and container resources — every quantity parses, a request
/// is at most its limit, and the quota holds twice each container limit and two pods, because a rolling update runs
/// the old and the new pod side by side.
/// </summary>
public sealed class PocTargetResources(PocTargetConfig target)
{
    private const string RollingUpdate = "a rolling update runs the old and the new pod side by side";

    public IEnumerable<string> Problems()
    {
        var quantities = Quantities().ToList();
        foreach (var (field, text) in quantities.Where(q => PocQuantity.Parse(q.Text) is null))
            yield return $"{Key(field)} is '{text}'; it must be a Kubernetes quantity such as 500m, 1, 128Mi or 1Gi, because "
                + "it is written into each PoC's manifests";
        if (target.Quota.Pods < 2)
            yield return $"{Key("quota.pods")} is {target.Quota.Pods}; it must be 2 or more, because {RollingUpdate}";
        if (quantities.Any(q => PocQuantity.Parse(q.Text) is null)) yield break;
        var quota = target.Quota;
        var container = target.Container;
        foreach (var (field, limit, quotaValue) in new[] { ("quota.cpu", container.CpuLimit, quota.Cpu),
            ("quota.memory", container.MemoryLimit, quota.Memory), ("quota.ephemeral_storage", container.EphemeralLimit, quota.EphemeralStorage) }
            .Where(f => PocQuantity.Parse(f.Item3) < 2 * PocQuantity.Parse(f.Item2)))
            yield return $"{Key(field)} is '{quotaValue}'; it must hold at least twice the container's limit ({limit}), "
                + $"because {RollingUpdate}";
        foreach (var (field, request, limit) in new[] { ("container.cpu_request", container.CpuRequest, container.CpuLimit),
            ("container.memory_request", container.MemoryRequest, container.MemoryLimit) }
            .Where(f => PocQuantity.Parse(f.Item2) > PocQuantity.Parse(f.Item3)))
            yield return $"{Key(field)} is '{request}'; it must be at most its limit ({limit}), because Kubernetes refuses "
                + "a container whose request exceeds its limit";
    }

    private string Key(string field) => $"pocs.targets[].{field} of target '{target.Name}'";

    private IEnumerable<(string Field, string Text)> Quantities() =>
    [
        ("quota.cpu", target.Quota.Cpu), ("quota.memory", target.Quota.Memory), ("quota.ephemeral_storage", target.Quota.EphemeralStorage),
        ("container.cpu_request", target.Container.CpuRequest), ("container.cpu_limit", target.Container.CpuLimit),
        ("container.memory_request", target.Container.MemoryRequest), ("container.memory_limit", target.Container.MemoryLimit),
        ("container.ephemeral_limit", target.Container.EphemeralLimit),
    ];
}
