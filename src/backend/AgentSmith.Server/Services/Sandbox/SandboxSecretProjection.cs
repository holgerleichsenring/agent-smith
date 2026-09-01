using AgentSmith.Contracts.Sandbox;
using k8s.Models;

namespace AgentSmith.Server.Services.Sandbox;

/// <summary>
/// p0272: turns the operator-declared credentials a <see cref="SandboxSpec"/> carries
/// into the pod's secretKeyRef env vars, Secret volumes and read-only file mounts.
/// Kubernetes resolves every value inside the pod, so no credential ever appears in a
/// Step payload, a Redis message or the model's view — which is why this projection is
/// its own responsibility and not part of the pod skeleton.
/// </summary>
public static class SandboxSecretProjection
{
    /// <summary>A secretKeyRef env entry. Shared by GIT_TOKEN and the sandbox.secrets env bindings.</summary>
    public static V1EnvVar EnvVar(string name, SecretRef source) => new()
    {
        Name = name,
        ValueFrom = new V1EnvVarSource
        {
            SecretKeyRef = new V1SecretKeySelector { Name = source.SecretName, Key = source.Key }
        }
    };

    /// <summary>Each declared secret file mounts read-only as a single file at its path.</summary>
    public static IEnumerable<V1VolumeMount> FileMounts(IReadOnlyList<SecretFileMount>? files) =>
        (files ?? []).Select((file, i) => new V1VolumeMount
        {
            Name = VolumeName(i),
            MountPath = file.MountPath,
            // subPath projects just that key, never a whole directory.
            SubPath = FileName(file.MountPath),
            ReadOnlyProperty = true
        });

    /// <summary>The Secret volumes the file mounts read from, in the same order.</summary>
    public static IEnumerable<V1Volume> FileVolumes(IReadOnlyList<SecretFileMount>? files) =>
        (files ?? []).Select((file, i) => new V1Volume
        {
            Name = VolumeName(i),
            Secret = new V1SecretVolumeSource
            {
                SecretName = file.Source.SecretName,
                Items = [new V1KeyToPath { Key = file.Source.Key, Path = FileName(file.MountPath) }]
            }
        });

    private static string VolumeName(int index) => $"secret-{index}";

    private static string FileName(string mountPath) => mountPath.Split('/')[^1];
}
