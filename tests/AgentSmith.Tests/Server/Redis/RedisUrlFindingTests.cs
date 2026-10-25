using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Core.Services.Configuration;
using AgentSmith.Server.Extensions;
using AgentSmith.Server.Services.Startup;
using FluentAssertions;
using Moq;
using StackExchange.Redis;

namespace AgentSmith.Tests.Server.Redis;

public sealed class RedisUrlFindingTests
{
    private const string Password = "s3cret-pass";

    [Theory]
    [InlineData("redis://:" + Password + "@redis:6379,abortConnect=maybe")]
    [InlineData("redis:6379,password=" + Password + ",abortConnect=maybe")]
    [InlineData("password=" + Password)]
    public void RedisExtensions_UnparseableUrl_FindingDoesNotContainTheValue(string redisUrl)
    {
        var findings = new StartupFindings();

        RedisExtensions.ParseOptions(redisUrl, findings);

        findings.All.Should().ContainSingle();
        findings.All[0].Reason.Should().NotContain(Password).And.NotContain(redisUrl);
    }

    [Fact]
    public async Task RedisProbe_ConnectionFailure_FindingDoesNotContainThePassword()
    {
        var infra = new Mock<IPreflightInfraProbe>();
        infra.Setup(p => p.ProbeRedisAsync(It.IsAny<CancellationToken>())).ReturnsAsync(
            ConnectionProbeResult.Unreachable(5, $"UnableToConnect on redis:6379,password={Password}"));
        var redis = new Mock<IConnectionMultiplexer>();
        redis.Setup(r => r.Configuration).Returns($"redis:6379,password={Password}");

        var findings = await new RedisProbe(infra.Object, redis.Object).ProbeAsync(CancellationToken.None);

        findings.Should().ContainSingle();
        findings[0].Reason.Should().NotContain(Password).And.Contain("redis:6379");
    }
}
