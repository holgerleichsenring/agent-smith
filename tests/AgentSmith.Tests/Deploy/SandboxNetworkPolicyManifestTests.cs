using FluentAssertions;
using YamlDotNet.RepresentationModel;
using static AgentSmith.Tests.Deploy.DeployManifests;

namespace AgentSmith.Tests.Deploy;

/// <summary>2026-10-25-c5a7d: the sandbox NetworkPolicy, pinned so a renamed label or a dropped except fails.</summary>
public sealed class SandboxNetworkPolicyManifestTests
{
    private static YamlMappingNode Policy() =>
        OfKind(Documents("k8s", "13-networkpolicy-sandbox.yaml"), "NetworkPolicy").Single();

    private static IReadOnlyList<YamlMappingNode> Egress() => [.. Items(Node(Policy(), "spec", "egress"))];

    private static IEnumerable<string> Strings(YamlNode? sequence) =>
        (sequence as YamlSequenceNode)?.Children.Select(c => c.ToString()) ?? [];

    [Fact]
    public void SandboxNetworkPolicyManifest_SelectsEveryPodInTheSandboxNamespace()
    {
        Scalar(Policy(), "metadata", "namespace").Should().Be("agentsmith-sandboxes");
        ((YamlMappingNode)Node(Policy(), "spec", "podSelector")!).Children.Should().BeEmpty();
        Strings(Node(Policy(), "spec", "policyTypes")).Should().BeEquivalentTo("Ingress", "Egress");
    }

    [Fact]
    public void SandboxNetworkPolicyManifest_HasNoIngressRule()
    {
        Node(Policy(), "spec", "ingress").Should().BeNull();
    }

    [Fact]
    public void SandboxNetworkPolicyManifest_RedisRule_MatchesRedisLabelsPortAndNamespace()
    {
        var redisLabels = Node(Documents("k8s", "6-deployment-redis.yaml")[0], "spec", "template", "metadata", "labels")!;
        var rule = Egress().Single(r => Items(Node(r, "to")).Any(t => Scalar(t, "podSelector", "matchLabels", "app") is not null
            && Scalar(t, "namespaceSelector", "matchLabels", "kubernetes.io/metadata.name") == "agentsmith"));
        var to = Items(Node(rule, "to")).Single();

        Scalar(to, "podSelector", "matchLabels", "app").Should().Be(Scalar(redisLabels, "app"));
        Scalar(Documents("k8s", "5-service-redis.yaml")[0], "metadata", "namespace").Should().Be("agentsmith");
        Items(Node(rule, "ports")).Select(p => Scalar(p, "port")).Should().Equal("6379");
    }

    [Fact]
    public void SandboxNetworkPolicyManifest_PublicRule_ExceptsPrivateLinkLocalSharedLoopbackAndWireServer()
    {
        var rule = Egress().Single(r => Items(Node(r, "to")).Any(t => Scalar(t, "ipBlock", "cidr") == "0.0.0.0/0"));

        Strings(Node(Items(Node(rule, "to")).Single(), "ipBlock", "except")).Should().BeEquivalentTo(
            "10.0.0.0/8", "172.16.0.0/12", "192.168.0.0/16", "100.64.0.0/10",
            "169.254.0.0/16", "127.0.0.0/8", "168.63.129.16/32");
        Items(Node(rule, "ports")).Select(p => Scalar(p, "port")).Should().BeEquivalentTo("80", "443");
    }

    [Fact]
    public void SandboxNetworkPolicyManifest_DnsRule_ReachesKubeDnsOn53()
    {
        var rule = Egress().Single(r => Items(Node(r, "to")).Any(t => Scalar(t, "podSelector", "matchLabels", "k8s-app") == "kube-dns"));

        Items(Node(rule, "ports")).Select(p => $"{Scalar(p, "protocol")}/{Scalar(p, "port")}")
            .Should().BeEquivalentTo("UDP/53", "TCP/53");
    }
}
