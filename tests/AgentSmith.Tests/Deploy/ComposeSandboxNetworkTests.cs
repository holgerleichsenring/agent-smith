using FluentAssertions;
using YamlDotNet.RepresentationModel;
using static AgentSmith.Tests.Deploy.DeployManifests;

namespace AgentSmith.Tests.Deploy;

/// <summary>2026-10-25-c5a7d: on compose, sandboxes leave the server's network and share one only with Redis.</summary>
public sealed class ComposeSandboxNetworkTests
{
    private const string Network = "agentsmith-sandbox";

    [Fact]
    public void ComposeSandboxNetwork_OnlyRedisJoinsIt_AndTheServerNamesIt()
    {
        var compose = Documents("docker-compose.example.yml").Single();
        var services = (YamlMappingNode)Node(compose, "services")!;

        var joined = services.Children
            .Where(kv => Networks(kv.Value).Contains(Network))
            .Select(kv => kv.Key.ToString());
        joined.Should().Equal("redis");

        var serverEnv = ((YamlSequenceNode)Node(services, "server", "environment")!).Children.Select(c => c.ToString());
        serverEnv.Should().Contain($"DOCKER_NETWORK={Network}");
        Scalar(compose, "networks", Network, "name").Should().Be(Network);
    }

    [Fact]
    public void ComposeSandboxNetwork_RedisPort_BindsLoopbackOnly()
    {
        var redis = Node(Documents("docker-compose.example.yml").Single(), "services", "redis")!;

        ((YamlSequenceNode)Node(redis, "ports")!).Children.Select(c => c.ToString())
            .Should().OnlyContain(p => p.StartsWith("127.0.0.1:"));
    }

    private static IEnumerable<string> Networks(YamlNode service) => Node(service, "networks") switch
    {
        YamlSequenceNode list => list.Children.Select(c => c.ToString()),
        YamlMappingNode map => map.Children.Keys.Select(k => k.ToString()),
        _ => [],
    };
}
