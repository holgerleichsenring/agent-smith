using YamlDotNet.RepresentationModel;
using static AgentSmith.Application.Services.Pocs.GitOps.PocYamlTree;

namespace AgentSmith.Application.Services.Pocs.GitOps;

/// <summary>
/// 2026-11-23-c81eb: what a render must hold before it is committed — the template is the operator's to edit, the
/// hardening is a ruling. Every resource sits in the PoC's namespace (the Namespace itself is named it), a NetworkPolicy
/// and a ResourceQuota exist, and every pod spec of a workload passes <see cref="PocPodPolicy"/>. Each refusal names the
/// file and the field.
/// </summary>
public sealed class PocManifestPolicy(PocPodPolicy pods)
{
    public IReadOnlyList<string> Check(IReadOnlyList<PocManifestFile> files, string @namespace)
    {
        ArgumentNullException.ThrowIfNull(files);
        var refusals = new List<string>();
        foreach (var file in files)
        foreach (var document in file.Documents)
        {
            refusals.AddRange(Placement(file, document, @namespace));
            if (PodSpec(document) is { } pod) refusals.AddRange(pods.Check(pod, $"{file.Name}: {Describe(document)}"));
        }
        foreach (var kind in new[] { "NetworkPolicy", "ResourceQuota" }.Where(k => !files.Any(f => f.Documents.Any(d => Text(d, "kind") == k))))
            refusals.Add($"the app template has no {kind}; every PoC namespace needs one");
        return refusals;
    }

    private static IEnumerable<string> Placement(PocManifestFile file, YamlMappingNode document, string @namespace)
    {
        var meta = MapOf(document, "metadata");
        var (field, value) = Text(document, "kind") == "Namespace"
            ? ("metadata.name", meta is null ? null : Text(meta, "name"))
            : ("metadata.namespace", meta is null ? null : Text(meta, "namespace"));
        if (value != @namespace)
            yield return $"{file.Name}: {Describe(document)} {field} is '{value}'; it must be '{@namespace}', because a PoC's "
                + "resources live in its own namespace";
    }

    /// <summary>The pod spec of a workload document, or null for a kind that runs no pod.</summary>
    private static YamlMappingNode? PodSpec(YamlMappingNode document) => Text(document, "kind") switch
    {
        "Pod" => MapOf(document, "spec") ?? new YamlMappingNode(),
        "Deployment" or "StatefulSet" or "DaemonSet" or "ReplicaSet" or "Job" =>
            MapOf(document, "spec", "template", "spec") ?? new YamlMappingNode(),
        "CronJob" => MapOf(document, "spec", "jobTemplate", "spec", "template", "spec") ?? new YamlMappingNode(),
        _ => null,
    };

    private static string Describe(YamlMappingNode document) =>
        $"{Text(document, "kind")} '{(MapOf(document, "metadata") is { } meta ? Text(meta, "name") : null)}'";
}
