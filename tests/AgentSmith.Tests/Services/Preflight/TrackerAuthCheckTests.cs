using AgentSmith.Application.Services.Preflight.Checks;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Preflight;
using AgentSmith.Contracts.Providers;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Moq;

namespace AgentSmith.Tests.Services.Preflight;

/// <summary>
/// p0324: tracker-auth proves each tracker credential with a real read AND verifies
/// the platform's webhook shared secret is configured — a missing secret means every
/// incoming webhook is silently rejected. Env access is injected so tests never
/// mutate process environment.
/// </summary>
public sealed class TrackerAuthCheckTests
{
    [Fact]
    public async Task TrackerAuthCheck_MissingWebhookScope_FailsActionable()
    {
        var config = ConfigWithTracker("ado", TrackerType.AzureDevOps);
        var check = new TrackerAuthCheck(
            FakePreflightConfigSource.Of(config),
            new StubTicketProviderFactory(),
            _ => null);

        var result = await check.RunAsync(CancellationToken.None);

        result.Status.Should().Be(PreflightStatus.Fail);
        result.Message.Should().Contain("AZDO_WEBHOOK_SECRET");
        result.FixHint.Should().Contain("webhook");
    }

    [Fact]
    public async Task RunAsync_AuthProbeFails_FailsActionable()
    {
        var provider = new Mock<ITicketProvider>();
        provider.Setup(p => p.ProbeAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ConnectionProbeResult.Unreachable(80, "403 token expired"));
        var factory = new Mock<ITicketProviderFactory>();
        factory.Setup(f => f.Create(It.IsAny<TrackerConnection>())).Returns(provider.Object);

        var check = new TrackerAuthCheck(
            FakePreflightConfigSource.Of(ConfigWithTracker("gh", TrackerType.GitHub)),
            factory.Object,
            _ => "secret-set");

        var result = await check.RunAsync(CancellationToken.None);

        result.Status.Should().Be(PreflightStatus.Fail);
        result.Message.Should().Contain("403 token expired");
        result.FixHint.Should().Contain("token");
    }

    [Fact]
    public async Task RunAsync_AuthOkAndSecretConfigured_Passes()
    {
        var check = new TrackerAuthCheck(
            FakePreflightConfigSource.Of(ConfigWithTracker("gh", TrackerType.GitHub)),
            new StubTicketProviderFactory(),
            _ => "secret-set");

        var result = await check.RunAsync(CancellationToken.None);

        result.Status.Should().Be(PreflightStatus.Pass);
        result.Message.Should().Contain("gh");
    }

    [Fact]
    public async Task RunAsync_JiraTrackerWithProjectSecret_Passes()
    {
        var config = ConfigWithTracker("jira", TrackerType.Jira);
        config.Projects["p"] = new ResolvedProject
        {
            Name = "p",
            JiraTrigger = new JiraTriggerConfig { Secret = "shhh" },
        };
        var check = new TrackerAuthCheck(
            FakePreflightConfigSource.Of(config), new StubTicketProviderFactory(), _ => null);

        var result = await check.RunAsync(CancellationToken.None);

        result.Status.Should().Be(PreflightStatus.Pass);
    }

    private static AgentSmithConfig ConfigWithTracker(string name, TrackerType type) => new()
    {
        Trackers = new Dictionary<string, TrackerConnection> { [name] = new() { Name = name, Type = type } },
    };
}
