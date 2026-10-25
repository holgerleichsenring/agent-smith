using AgentSmith.Server.Services.Sandbox;
using FluentAssertions;

namespace AgentSmith.Tests.Sandbox;

/// <summary>2026-10-25-c5a7b: the reader taken out of the registrations reads what they read.</summary>
public sealed class DockerSandboxOptionsReaderTests
{
    [Fact]
    public void DockerSandboxOptionsReader_ReadsTheExistingVariables()
    {
        var env = new Dictionary<string, string>
        {
            ["REDIS_URL"] = "redis.internal:6380",
            ["DOCKER_HOST"] = "tcp://docker:2375",
            ["DOCKER_NETWORK"] = "agentsmith-sandbox",
            ["SANDBOX_PACKAGE_CACHE"] = "false",
        };

        var options = new DockerSandboxOptionsReader(name => env.GetValueOrDefault(name)).Read();

        options.RedisUrl.Should().Be("redis.internal:6380");
        options.DockerSocketUri.Should().Be("tcp://docker:2375");
        options.Network.Should().Be("agentsmith-sandbox");
        options.PackageCacheEnabled.Should().BeFalse();
    }

    [Fact]
    public void DockerSandboxOptionsReader_Unset_KeepsTheDefaults()
    {
        var options = new DockerSandboxOptionsReader(_ => null).Read();

        options.RedisUrl.Should().Be("redis:6379");
        options.DockerSocketUri.Should().Be(DockerSocketUriResolver.DefaultSocket);
        options.Network.Should().BeEmpty();
        options.PackageCacheEnabled.Should().BeTrue();
    }
}
