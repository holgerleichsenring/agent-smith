using FluentAssertions;
using YamlDotNet.RepresentationModel;
using static AgentSmith.Tests.Deploy.DeployManifests;

namespace AgentSmith.Tests.Deploy;

/// <summary>2026-10-25-c5a7c: the reference manifests ship sandbox pods in a namespace of their own.</summary>
public sealed class SandboxNamespaceManifestTests
{
    private const string SandboxNamespace = "agentsmith-sandboxes";

    public static TheoryData<string> ServerDeployments => new()
    {
        "k8s/8-deployment-server.yaml",
        "k8s/examples/airgap-deployment.yaml",
        "k8s/examples/path-mode-deployment.yaml",
    };

    [Theory]
    [MemberData(nameof(ServerDeployments))]
    public void ReferenceManifests_EveryServerDeploymentNamesTheSandboxNamespace(string file)
    {
        var env = ServerEnv(file);

        env.Should().Contain(e => Scalar(e, "name") == "K8S_SANDBOX_NAMESPACE" && Scalar(e, "value") == SandboxNamespace);
        env.Should().Contain(e => Scalar(e, "name") == "SANDBOX_REDIS_URL"
            && Scalar(e, "valueFrom", "secretKeyRef", "key") == "sandbox-redis-url");
    }

    [Fact]
    public void ReferenceManifests_ARoleGrantsPodsInTheSandboxNamespace_ToTheServerAccount()
    {
        var rbac = Documents("k8s", "2-rbac.yaml");
        var role = OfKind(rbac, "Role").Single(r => Scalar(r, "metadata", "namespace") == SandboxNamespace);
        Items(Node(role, "rules")).Should().Contain(rule =>
            ((YamlSequenceNode)Node(rule, "resources")!).Children.Any(c => c.ToString() == "pods")
            && ((YamlSequenceNode)Node(rule, "verbs")!).Children.Any(c => c.ToString() == "create"));

        var binding = OfKind(rbac, "RoleBinding").Single(b => Scalar(b, "metadata", "namespace") == SandboxNamespace);
        Scalar(binding, "roleRef", "name").Should().Be(Scalar(role, "metadata", "name"));
        Items(Node(binding, "subjects")).Should().Contain(s =>
            Scalar(s, "name") == "agentsmith" && Scalar(s, "namespace") == "agentsmith");
        OfKind(Documents("k8s", "1-namespace.yaml"), "Namespace")
            .Should().Contain(n => Scalar(n, "metadata", "name") == SandboxNamespace);
    }

    private static IReadOnlyList<YamlMappingNode> ServerEnv(string file)
    {
        var deployment = OfKind(Documents(file.Split('/')), "Deployment")
            .Single(d => Scalar(d, "metadata", "name") == "agentsmith-server");
        var server = Items(Node(deployment, "spec", "template", "spec", "containers"))
            .Single(c => Scalar(c, "name") == "server");
        return [.. Items(Node(server, "env"))];
    }
}
