using AgentSmith.Server.Services.Sandbox;
using AgentSmith.Server.Services.Startup;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using StackExchange.Redis;

namespace AgentSmith.Tests.Sandbox;

public sealed class SandboxBusCredentialProbeTests
{
    [Fact]
    public async Task SandboxBusCredentialProbe_DefaultUserNoPass_WarnsThatPerJobUsersProtectNothing()
    {
        var redis = new SandboxBusRedisDouble();
        redis.Database.Setup(d => d.ExecuteAsync("ACL", It.IsAny<object[]>())).ReturnsAsync(RedisResult.Create(
        [
            RedisResult.Create((RedisValue)"flags"),
            RedisResult.Create([RedisResult.Create((RedisValue)"on"), RedisResult.Create((RedisValue)"nopass")]),
        ]));

        var findings = await Probe(redis, SandboxBusCredentialMode.PerJob).ProbeAsync(CancellationToken.None);

        findings.Should().ContainSingle().Which.Reason.Should().Contain("protect nothing");
    }

    [Fact]
    public async Task SandboxBusCredentialProbe_DefaultUserWithPassword_HasNoFinding()
    {
        var redis = new SandboxBusRedisDouble();
        redis.Database.Setup(d => d.ExecuteAsync("ACL", It.IsAny<object[]>())).ReturnsAsync(RedisResult.Create(
        [
            RedisResult.Create((RedisValue)"flags"),
            RedisResult.Create([RedisResult.Create((RedisValue)"on")]),
        ]));

        var findings = await Probe(redis, SandboxBusCredentialMode.PerJob).ProbeAsync(CancellationToken.None);

        findings.Should().BeEmpty();
    }

    [Fact]
    public async Task SandboxBusCredentialProbe_AclRefused_NamesTheFallback()
    {
        var redis = new SandboxBusRedisDouble(new RedisServerException("NOPERM no ACL rights"));

        var findings = await Probe(redis, SandboxBusCredentialMode.PerJob).ProbeAsync(CancellationToken.None);

        findings.Should().ContainSingle().Which.Reason.Should().Contain("shared Redis credential");
    }

    [Fact]
    public async Task SandboxBusCredentialProbe_Shared_NamesTheChoice()
    {
        var findings = await Probe(new SandboxBusRedisDouble(), SandboxBusCredentialMode.Shared)
            .ProbeAsync(CancellationToken.None);

        findings.Should().ContainSingle().Which.Field.Should().Be("SANDBOX_BUS_CREDENTIAL");
    }

    private static SandboxBusCredentialProbe Probe(SandboxBusRedisDouble redis, SandboxBusCredentialMode mode) =>
        new(redis.Multiplexer.Object, new SandboxBusCredentialSetting(mode), NullLogger<SandboxBusCredentialProbe>.Instance);
}
