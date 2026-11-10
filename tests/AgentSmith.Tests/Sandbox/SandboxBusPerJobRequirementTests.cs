using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Server.Services.Sandbox;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using k8s;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using StackExchange.Redis;

namespace AgentSmith.Tests.Sandbox;

/// <summary>
/// 2026-11-10-4778 review: a spawn that requires its own bus credential is refused where the credential is issued —
/// before Docker or Kubernetes create anything — when the installation is on the shared credential or Redis refuses
/// ACL SETUSER for this one job.
/// </summary>
public sealed class SandboxBusPerJobRequirementTests
{
    private static IConnectionMultiplexer RefusingRedis()
    {
        var db = new Mock<IDatabase>();
        db.Setup(d => d.ExecuteAsync(It.IsAny<string>(), It.IsAny<object[]>()))
            .ThrowsAsync(new RedisServerException("NOPERM this user has no permissions to run the 'acl' command"));
        var redis = new Mock<IConnectionMultiplexer>();
        redis.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(db.Object);
        return redis.Object;
    }

    [Fact]
    public async Task SandboxBusCredentials_SetUserRefused_RequirePerJob_Throws_OtherwiseFallsBackToShared()
    {
        var credentials = SandboxBusDoubles.Credentials(SandboxBusCredentialMode.PerJob, RefusingRedis());

        var required = () => credentials.IssueAsync("job-1", "redis:6379", requirePerJob: true);

        (await required.Should().ThrowAsync<InvalidOperationException>()).WithMessage("*shared one*");
        (await credentials.IssueAsync("job-2", "redis:6379")).IsPerJob.Should().BeFalse("a coding run keeps working on the fallback");
    }

    [Fact]
    public async Task SandboxBusCredentials_SharedInstallation_RequirePerJob_Throws()
    {
        var required = () => SandboxBusDoubles.Credentials().IssueAsync("job-1", "redis:6379", requirePerJob: true);

        await required.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task KubernetesSandboxFactory_RequiresPerJobBus_SharedCredential_FailsClosedBeforeThePod()
    {
        var options = new KubernetesSandboxOptionsReader(name => name switch
        {
            "K8S_NAMESPACE" => "agentsmith", "REDIS_URL" => "redis:6379", _ => null,
        }).Read();
        var client = new Mock<IKubernetes>(MockBehavior.Strict);
        var factory = new KubernetesSandboxFactory(client.Object, Mock.Of<IConnectionMultiplexer>(),
            new PodSpecBuilder(new SandboxPodLabels(new SandboxOwnerIdentity("store-test")), new PodSecurity(options)),
            options, Options.Create(new SandboxGlobalConfig()), Mock.Of<IWireProtocolWatcher>(),
            SandboxBusDoubles.Credentials(SandboxBusCredentialMode.PerJob, RefusingRedis()), NullLoggerFactory.Instance);

        var act = () => factory.CreateAsync(
            new SandboxSpec("node:20", ResourceLimits.Default, RequiresPerJobBus: true), CancellationToken.None);

        (await act.Should().ThrowAsync<InvalidOperationException>()).WithMessage("*shared one*");
        client.VerifyNoOtherCalls();
    }
}
