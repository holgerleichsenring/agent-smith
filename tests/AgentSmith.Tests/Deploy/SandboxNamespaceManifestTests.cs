using FluentAssertions;
using YamlDotNet.RepresentationModel;
using static AgentSmith.Tests.Deploy.DeployManifests;

namespace AgentSmith.Tests.Deploy;

/// <summary>
/// 2026-10-25-c5a7c, 2026-10-25-e41d: sandbox pods may run in a namespace of their own, but only
/// through the opt-in folder; applying deploy/k8s/ keeps them where they run today.
/// </summary>
public sealed class SandboxNamespaceManifestTests
{
    private const string SandboxNamespace = "agentsmith-sandboxes";
    private const string IsolationFolder = "sandbox-isolation";

    public static TheoryData<string> ServerDeployments => new()
    {
        "k8s/8-deployment-server.yaml",
        "k8s/examples/airgap-deployment.yaml",
        "k8s/examples/path-mode-deployment.yaml",
    };

    [Theory]
    [MemberData(nameof(ServerDeployments))]
    public void SandboxNamespaceManifest_DefaultServerDeployments_SetNeitherSandboxVariable(string file)
    {
        ServerEnv(file).Select(e => Scalar(e, "name"))
            .Should().NotContain(["K8S_SANDBOX_NAMESPACE", "SANDBOX_REDIS_URL"]);
    }

    [Fact]
    public void SandboxNamespaceManifest_DefaultManifests_CreateNothingInTheSandboxNamespace()
    {
        var defaults = Directory.GetFiles(Path("k8s"), "*.yaml")
            .SelectMany(f => Documents("k8s", System.IO.Path.GetFileName(f)))
            .ToList();

        defaults.Should().NotContain(d => Scalar(d, "metadata", "namespace") == SandboxNamespace);
        OfKind(defaults, "Namespace").Should().NotContain(n => Scalar(n, "metadata", "name") == SandboxNamespace);
        OfKind(defaults, "NetworkPolicy").Should().BeEmpty();
    }

    [Fact]
    public void SandboxNamespaceManifest_IsolationFolder_GrantsPodsInTheSandboxNamespace()
    {
        var rbac = Documents("k8s", IsolationFolder, "2-rbac-sandboxes.yaml");
        var role = OfKind(rbac, "Role").Single(r => Scalar(r, "metadata", "namespace") == SandboxNamespace);
        Items(Node(role, "rules")).Should().Contain(rule =>
            ((YamlSequenceNode)Node(rule, "resources")!).Children.Any(c => c.ToString() == "pods")
            && ((YamlSequenceNode)Node(rule, "verbs")!).Children.Any(c => c.ToString() == "create"));
        Items(Node(role, "rules")).Should().Contain(rule =>
            ((YamlSequenceNode)Node(rule, "resources")!).Children.Any(c => c.ToString() == "resourcequotas"));

        var binding = OfKind(rbac, "RoleBinding").Single(b => Scalar(b, "metadata", "namespace") == SandboxNamespace);
        Scalar(binding, "roleRef", "name").Should().Be(Scalar(role, "metadata", "name"));
        Items(Node(binding, "subjects")).Should().Contain(s =>
            Scalar(s, "name") == "agentsmith" && Scalar(s, "namespace") == "agentsmith");
        OfKind(Documents("k8s", IsolationFolder, "1-namespace-sandboxes.yaml"), "Namespace")
            .Should().ContainSingle(n => Scalar(n, "metadata", "name") == SandboxNamespace);
    }

    [Fact]
    public void SandboxNamespaceManifest_IsolationReadme_NamesBothServerVariables()
    {
        var readme = File.ReadAllText(Path("k8s", IsolationFolder, "README.md"));

        readme.Should().Contain("K8S_SANDBOX_NAMESPACE").And.Contain("SANDBOX_REDIS_URL").And.Contain("sandbox-redis-url");
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
