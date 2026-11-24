using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Pocs;
using YamlDotNet.RepresentationModel;
using static AgentSmith.Application.Services.Pocs.GitOps.PocYamlTree;

namespace AgentSmith.Application.Services.Pocs.GitOps;

/// <summary>
/// 2026-11-23-c81eb: sets the fields agent smith owns on the Deployment 'app' as typed nodes — replicas a plain integer,
/// the sync wave after the sealed secrets, the pod labels the selectors match, the variables checksum, the pull secret,
/// and on container 'app' the image by digest, the readiness probe, envFrom and the proxy variables.
/// </summary>
public sealed class PocDeploymentFields(PocContainerFields container)
{
    /// <summary>Null when set, else why the Deployment cannot carry them.</summary>
    public string? Apply(YamlMappingNode deployment, PocRenderInput input)
    {
        ArgumentNullException.ThrowIfNull(deployment);
        ArgumentNullException.ThrowIfNull(input);
        Set(Map(Map(deployment, "metadata"), "annotations"), PocManifestNames.SyncWave, PocYamlText.Text(PocManifestNames.DeploymentWave));
        var spec = Map(deployment, "spec");
        Set(spec, "replicas", PocYamlText.Number(input.Desired.Replicas));
        Labels(Map(Map(spec, "selector"), "matchLabels"), input.Name);
        var template = Map(spec, "template");
        var metadata = Map(template, "metadata");
        Labels(Map(metadata, "labels"), input.Name);
        Set(Map(metadata, "annotations"), PocManifestNames.ChecksumAnnotation, PocYamlText.Text(input.Seal.VariablesChecksum));
        var pod = Map(template, "spec");
        PullSecret(pod, input.Target);
        return Items(pod, "containers").FirstOrDefault(c => Text(c, "name") == PocManifestNames.App) is { } app
            ? container.Apply(app, input)
            : $"the Deployment '{PocManifestNames.App}' has no container '{PocManifestNames.App}' (spec.template.spec.containers)";
    }

    private static void Labels(YamlMappingNode labels, string poc)
    {
        Set(labels, PocManifestNames.NameLabel, PocYamlText.Text(PocManifestNames.App));
        Set(labels, PocManifestNames.PocLabel, PocYamlText.Text(poc));
    }

    private static void PullSecret(YamlMappingNode pod, PocTargetConfig target)
    {
        if (target.Registry?.PullMode != PocRegistryConfig.PullSecretMode)
        {
            Remove(pod, "imagePullSecrets");
            return;
        }
        Set(pod, "imagePullSecrets", new YamlSequenceNode(new YamlMappingNode { { "name", PocYamlText.Text(PocSecretNames.Pull) } }));
    }
}
