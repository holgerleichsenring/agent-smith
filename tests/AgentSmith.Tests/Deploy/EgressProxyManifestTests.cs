using FluentAssertions;
using YamlDotNet.RepresentationModel;
using static AgentSmith.Tests.Deploy.DeployManifests;

namespace AgentSmith.Tests.Deploy;

/// <summary>
/// 2026-10-25-c5a7g: the opt-in egress proxy. Its policy must replace the sandbox-isolation policy
/// (2026-10-25-e41d), not sit beside it, because policies are additive and a second one would leave
/// 80 and 443 open.
/// </summary>
public sealed class EgressProxyManifestTests
{
    [Fact]
    public void EgressProxyExample_PolicyHasTheIsolationPolicyNameAndNamespace_AndAllowsOnlyProxyDnsAndRedis()
    {
        var shipped = OfKind(Documents("k8s", "sandbox-isolation", "3-networkpolicy-sandbox.yaml"), "NetworkPolicy").Single();
        var example = Documents("k8s", "examples", "sandbox-egress-proxy.yaml");
        var policy = OfKind(example, "NetworkPolicy").Single();

        Scalar(policy, "metadata", "name").Should().Be(Scalar(shipped, "metadata", "name"));
        Scalar(policy, "metadata", "namespace").Should().Be(Scalar(shipped, "metadata", "namespace"));
        Node(policy, "spec", "ingress").Should().BeNull();

        var targets = Items(Node(policy, "spec", "egress"))
            .SelectMany(rule => Items(Node(rule, "to")).Select(to => (To: to, Ports: Ports(rule))))
            .ToList();
        targets.Should().OnlyContain(t => Scalar(t.To, "ipBlock", "cidr") == null, "no address range is open");
        targets.Select(t => Scalar(t.To, "podSelector", "matchLabels", "k8s-app") ?? Scalar(t.To, "podSelector", "matchLabels", "app"))
            .Should().BeEquivalentTo("kube-dns", "redis", "agentsmith-egress-proxy");

        var service = OfKind(example, "Service").Single();
        var proxy = targets.Single(t => Scalar(t.To, "podSelector", "matchLabels", "app") == "agentsmith-egress-proxy");
        proxy.Ports.Should().Equal(Scalar(Items(Node(service, "spec", "ports")).Single(), "port"));
        Scalar(service, "spec", "selector", "app").Should().Be("agentsmith-egress-proxy");
    }

    [Fact]
    public void EgressProxyExample_SaysItRequiresTheSandboxIsolationFolder()
    {
        File.ReadAllText(Path("k8s", "examples", "sandbox-egress-proxy.yaml"))
            .Should().Contain("deploy/k8s/sandbox-isolation/");
    }

    [Fact]
    public void EgressProxyExample_ImageIsPinnedByVersionAndDigest()
    {
        var deployment = OfKind(Documents("k8s", "examples", "sandbox-egress-proxy.yaml"), "Deployment").Single();
        var image = Scalar(Items(Node(deployment, "spec", "template", "spec", "containers")).Single(), "image")!;

        image.Should().MatchRegex(@"^[^:@]+:[^@]+@sha256:[0-9a-f]{64}$");
    }

    [Fact]
    public void ComposeEgressOverride_SandboxNetworkInternal_ProxyAttached()
    {
        var compose = Documents("docker-compose.egress-proxy.yml").Single();

        Scalar(compose, "networks", "agentsmith-sandbox", "name").Should().Be("agentsmith-sandbox");
        Scalar(compose, "networks", "agentsmith-sandbox", "internal").Should().Be("true");
        ((YamlSequenceNode)Node(compose, "services", "egress-proxy", "networks")!).Children.Select(c => c.ToString())
            .Should().BeEquivalentTo("default", "agentsmith-sandbox");
        ((YamlSequenceNode)Node(compose, "services", "server", "environment")!).Children.Select(c => c.ToString())
            .Should().Contain("SANDBOX_EGRESS_PROXY=http://egress-proxy:3128");
        Node(compose, "services", "server", "networks").Should().BeNull("the server stays off the sandbox network");
    }

    private static List<string?> Ports(YamlMappingNode rule) =>
        [.. Items(Node(rule, "ports")).Select(p => Scalar(p, "port"))];
}
