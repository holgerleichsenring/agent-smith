using AgentSmith.Server.Services.Sandbox;
using FluentAssertions;

namespace AgentSmith.Tests.Sandbox;

/// <summary>
/// The Kubernetes sandbox backend's client outside a cluster. It used to be the job spawner's
/// kubeconfig registration that won; with the spawner gone the sandbox registers it itself.
/// </summary>
public sealed class KubernetesClientConfigLoaderTests : IDisposable
{
    private readonly string _kubeconfig = Path.Combine(Path.GetTempPath(), $"kubeconfig-{Guid.NewGuid():N}");

    public void Dispose() => File.Delete(_kubeconfig);

    [Fact]
    public void KubernetesSandbox_OutOfCluster_UsesKubeconfig()
    {
        File.WriteAllText(_kubeconfig, Kubeconfig("https://kube.sample.test:6443"));

        var config = new KubernetesClientConfigLoader(inCluster: false, _kubeconfig).Load();

        config.Host.Should().Be("https://kube.sample.test:6443");
        config.Namespace.Should().Be("sandboxes");
    }

    [Fact]
    public void KubernetesSandbox_LocalKubeconfig_ReachesDockerDesktopFromAContainer()
    {
        File.WriteAllText(_kubeconfig, Kubeconfig("https://127.0.0.1:6443"));

        var config = new KubernetesClientConfigLoader(inCluster: false, _kubeconfig).Load();

        config.Host.Should().Be("https://host.docker.internal:6443");
    }

    private static string Kubeconfig(string server) => $"""
        apiVersion: v1
        kind: Config
        current-context: local
        clusters:
        - name: local
          cluster:
            server: {server}
            insecure-skip-tls-verify: true
        contexts:
        - name: local
          context:
            cluster: local
            user: local
            namespace: sandboxes
        users:
        - name: local
          user:
            token: sample-token
        """;
}
