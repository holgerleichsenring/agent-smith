using YamlDotNet.RepresentationModel;
using static AgentSmith.Application.Services.Pocs.GitOps.PocYamlTree;

namespace AgentSmith.Application.Services.Pocs.GitOps;

/// <summary>
/// 2026-11-23-c81eb: the hardening of one pod spec — no service account token, no host network, PID or IPC, no
/// hostPath volume, an emptyDir mounted at /tmp, and on EVERY container and initContainer runAsNonRoot, runAsUser 10001,
/// a read-only root filesystem, no privilege escalation, capabilities drop ALL, and cpu and memory limits.
/// </summary>
public sealed class PocPodPolicy
{
    public const string RunAsUser = "10001";

    public IEnumerable<string> Check(YamlMappingNode pod, string where)
    {
        ArgumentNullException.ThrowIfNull(pod);
        if (Text(pod, "automountServiceAccountToken") != "false")
            yield return $"{where} spec.automountServiceAccountToken must be false, because a PoC talks to no Kubernetes API";
        foreach (var host in new[] { "hostNetwork", "hostPID", "hostIPC" }.Where(h => Text(pod, h) == "true"))
            yield return $"{where} spec.{host} must not be true, because a PoC shares nothing with its node";
        var volumes = Items(pod, "volumes").ToList();
        foreach (var volume in volumes.Where(v => Get(v, "hostPath") is not null))
            yield return $"{where} spec.volumes[{Text(volume, "name")}].hostPath is not allowed, because a PoC reads nothing of its node";
        var scratch = volumes.Where(v => Get(v, "emptyDir") is not null).Select(v => Text(v, "name")).ToHashSet();
        var containers = Items(pod, "containers").Select(c => ("containers", c)).Concat(Items(pod, "initContainers").Select(c => ("initContainers", c))).ToList();
        if (!containers.Any(c => Items(c.Item2, "volumeMounts").Any(m => Text(m, "mountPath") == "/tmp" && scratch.Contains(Text(m, "name")))))
            yield return $"{where} spec.volumes needs an emptyDir mounted at /tmp, because the root filesystem is read-only";
        foreach (var (list, container) in containers)
        foreach (var refusal in Container(container, $"{where} spec.{list}[{Text(container, "name")}]"))
            yield return refusal;
    }

    private static IEnumerable<string> Container(YamlMappingNode container, string where)
    {
        var security = MapOf(container, "securityContext") ?? new YamlMappingNode();
        foreach (var (field, wanted) in new[] { ("runAsNonRoot", "true"), ("runAsUser", RunAsUser), ("readOnlyRootFilesystem", "true"),
            ("allowPrivilegeEscalation", "false") }.Where(f => Text(security, f.Item1) != f.Item2))
            yield return $"{where}.securityContext.{field} must be {wanted}, because the PoC hardening is not optional";
        var drop = MapOf(security, "capabilities") is { } caps && Get(caps, "drop") is YamlSequenceNode list
            ? list.Children.OfType<YamlScalarNode>().Select(s => s.Value) : [];
        if (!drop.Contains("ALL"))
            yield return $"{where}.securityContext.capabilities.drop must hold ALL, because the PoC hardening is not optional";
        var limits = MapOf(container, "resources", "limits") ?? new YamlMappingNode();
        foreach (var resource in new[] { "cpu", "memory" }.Where(r => string.IsNullOrWhiteSpace(Text(limits, r))))
            yield return $"{where}.resources.limits.{resource} must be set, because the namespace's quota counts limits";
    }
}
