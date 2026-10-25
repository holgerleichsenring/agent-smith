using System.Net;
using AgentSmith.Infrastructure.Core.Services.Configuration;
using AgentSmith.Sandbox.Wire;
using AgentSmith.Server.Services.Sandbox;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using StackExchange.Redis;

namespace AgentSmith.Tests.Sandbox;

public sealed class SandboxBusCredentialsTests
{
    private const string ServerUrl = "redis:6379,password=server-secret";

    [Fact]
    public async Task SandboxBusCredentials_Url_CarriesUserPasswordAndBlankTiebreakerAndConfigChannel()
    {
        var redis = new SandboxBusRedisDouble();

        await using var credential = await Credentials(redis).IssueAsync("job1", ServerUrl);

        var parsed = ConfigurationOptions.Parse(credential.Url);
        parsed.User.Should().Be("sbx-job1");
        parsed.Password.Should().NotBeNullOrEmpty().And.NotBe("server-secret");
        parsed.TieBreaker.Should().BeEmpty();
        parsed.ConfigurationChannel.Should().BeEmpty();
        parsed.EndPoints.Should().ContainSingle().Which.Should().Be(new DnsEndPoint("redis", 6379));
        credential.Url.Should().NotContain("server-secret");
    }

    [Fact]
    public async Task SandboxBusCredentials_SetUser_ScopesToTheJobPatternAndActiveRunsReadOnly()
    {
        var redis = new SandboxBusRedisDouble();

        await using var credential = await Credentials(redis).IssueAsync("job1", ServerUrl);

        var args = redis.Calls("SETUSER").Should().ContainSingle().Subject.Select(a => a.ToString()).ToList();
        args.Take(3).Should().Equal("SETUSER", "sbx-job1", "reset");
        args.Should().Contain(["on", "~sandbox:job1:*", "%R~agentsmith:runs:active", "resetchannels", "-@all"]);
        args.Should().Contain(">" + ConfigurationOptions.Parse(credential.Url).Password);
        args.Should().NotContain(a => a.StartsWith('~') && a != "~sandbox:job1:*");
        args.Where(a => a.StartsWith('+')).Should().BeEquivalentTo(SandboxBusAcl.Commands.Select(c => "+" + c));
    }

    [Fact]
    public async Task SandboxBusCredentials_AclRefused_FallsBackToSharedWithAFindingNamingTheVariable()
    {
        var redis = new SandboxBusRedisDouble(new RedisServerException("ERR unknown command 'ACL'"));
        var findings = new StartupFindings();

        var credential = await Credentials(redis, findings: findings).IssueAsync("job1", ServerUrl);

        credential.Url.Should().Be(ServerUrl);
        credential.IsPerJob.Should().BeFalse();
        findings.All.Should().ContainSingle().Which.Reason.Should()
            .Contain("SANDBOX_BUS_CREDENTIAL").And.Contain("unknown command");
    }

    [Fact]
    public async Task SandboxBusCredentials_Shared_HandsTheServerUrl()
    {
        var redis = new SandboxBusRedisDouble();

        var credential = await Credentials(redis, SandboxBusCredentialMode.Shared).IssueAsync("job1", ServerUrl);
        await credential.DisposeAsync();

        credential.Url.Should().Be(ServerUrl);
        redis.AclCalls.Should().BeEmpty();
    }

    [Fact]
    public async Task SandboxBusCredentials_Dispose_DeletesTheUser()
    {
        var redis = new SandboxBusRedisDouble();

        var credential = await Credentials(redis).IssueAsync("job1", ServerUrl);
        await credential.DisposeAsync();

        redis.Calls("DELUSER").Should().ContainSingle().Which.Should().Equal("DELUSER", "sbx-job1");
    }

    [Fact]
    public async Task SandboxBusCredentials_ConnectionRestored_ReissuesLiveUsers()
    {
        var redis = new SandboxBusRedisDouble();
        var credentials = Credentials(redis);
        await using var live = await credentials.IssueAsync("live", ServerUrl);
        await (await credentials.IssueAsync("gone", ServerUrl)).DisposeAsync();
        var reissued = new TaskCompletionSource();
        redis.Database.Invocations.Clear();
        redis.Database.Setup(d => d.ExecuteAsync("ACL", It.IsAny<object[]>()))
            .Callback<string, object[]>((_, args) => { redis.AclCalls.Add(args); reissued.TrySetResult(); })
            .ReturnsAsync(RedisResult.Create((RedisValue)"OK"));
        var before = redis.Calls("SETUSER").Count();

        redis.Multiplexer.Raise(m => m.ConnectionRestored += null, new ConnectionFailedEventArgs(
            null!, new DnsEndPoint("redis", 6379), ConnectionType.Interactive, ConnectionFailureType.None, null!, ""));
        await reissued.Task.OrHang("the re-issue on ConnectionRestored");

        var after = redis.Calls("SETUSER").Skip(before).Select(a => (string)a[1]).ToList();
        after.Should().Equal("sbx-live");
    }

    internal static SandboxBusCredentials Credentials(SandboxBusRedisDouble redis,
        SandboxBusCredentialMode mode = SandboxBusCredentialMode.PerJob, StartupFindings? findings = null) =>
        new(redis.Multiplexer.Object, new SandboxBusCredentialSetting(mode), findings ?? new StartupFindings(),
            NullLogger<SandboxBusCredentials>.Instance);
}
