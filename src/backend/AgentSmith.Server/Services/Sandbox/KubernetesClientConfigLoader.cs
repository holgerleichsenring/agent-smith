using k8s;

namespace AgentSmith.Server.Services.Sandbox;

/// <summary>
/// The Kubernetes client configuration the sandbox backend talks through: the in-cluster
/// service account when the server runs in a pod, otherwise the kubeconfig at
/// <paramref name="kubeConfigPath"/>. A kubeconfig pointing at 127.0.0.1 is read as Docker
/// Desktop's cluster, which a containerised server reaches at host.docker.internal.
/// </summary>
public sealed class KubernetesClientConfigLoader(bool inCluster, string kubeConfigPath)
{
    private const string LocalApi = "https://127.0.0.1:";
    private const string DockerDesktopApi = "https://host.docker.internal:";

    /// <summary>The loader for the process it runs in: in-cluster detection and the default kubeconfig.</summary>
    public static KubernetesClientConfigLoader ForThisProcess() => new(
        KubernetesClientConfiguration.IsInCluster(),
        KubernetesClientConfiguration.KubeConfigDefaultLocation);

    public KubernetesClientConfiguration Load()
    {
        if (inCluster) return KubernetesClientConfiguration.InClusterConfig();
        var yaml = File.ReadAllText(kubeConfigPath).Replace(LocalApi, DockerDesktopApi);
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(yaml));
        return KubernetesClientConfiguration.BuildConfigFromConfigFile(stream);
    }
}
