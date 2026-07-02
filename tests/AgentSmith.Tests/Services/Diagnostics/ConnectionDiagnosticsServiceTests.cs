using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Server.Contracts;
using AgentSmith.Server.Services.Diagnostics;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Services.Diagnostics;

/// <summary>
/// p0292/p0293: ConnectionDiagnosticsService enumerates repos + trackers + agents +
/// infra (redis/persistence/sandbox) + configured chat adapters, each with the right
/// kind + category, skips Local repos and unconfigured chat, and never leaks a secret.
/// </summary>
public sealed class ConnectionDiagnosticsServiceTests
{
    [Fact]
    public async Task GetSnapshotAsync_ListsServicesAgentsAndInfra_SkipsLocalAndUnconfiguredChat()
    {
        var sut = CreateSut(BuildConfig(), chatConfigured: false);

        var snapshot = await sut.GetSnapshotAsync(CancellationToken.None);

        snapshot.Connections.Select(c => c.Name)
            .Should().BeEquivalentTo("gh", "jira", "claude-x", "redis", "persistence", "sandbox");
        snapshot.Connections.Single(c => c.Name == "gh").Category.Should().Be("service");
        snapshot.Connections.Single(c => c.Name == "claude-x").Kind.Should().Be("agent");
        snapshot.Connections.Single(c => c.Name == "redis").Category.Should().Be("infra");
    }

    [Fact]
    public async Task GetSnapshotAsync_SlackConfigured_AddsChatRow()
    {
        var sut = CreateSut(BuildConfig(), chatConfigured: true);

        var snapshot = await sut.GetSnapshotAsync(CancellationToken.None);

        var slack = snapshot.Connections.Single(c => c.Name == "slack");
        slack.Kind.Should().Be("chat");
        slack.Category.Should().Be("chat");
    }

    [Fact]
    public async Task GetSnapshotAsync_JiraProjectSecret_ReportsSecretConfiguredAndLastSeen()
    {
        var seen = new Dictionary<string, DateTimeOffset> { ["jira"] = DateTimeOffset.UnixEpoch };
        var sut = CreateSut(BuildConfig(), chatConfigured: false, seen);

        var snapshot = await sut.GetSnapshotAsync(CancellationToken.None);

        var jira = snapshot.Webhooks.Single(w => w.Platform == "jira");
        jira.SecretConfigured.Should().BeTrue();
        jira.LastReceivedUtc.Should().Be(DateTimeOffset.UnixEpoch);
    }

    [Fact]
    public async Task ProbeAsync_UnknownName_ReturnsNull()
    {
        var sut = CreateSut(BuildConfig(), chatConfigured: false);

        (await sut.ProbeAsync("does-not-exist", CancellationToken.None)).Should().BeNull();
    }

    [Fact]
    public async Task ProbeAsync_KnownRepo_ReturnsOkStatusWithKind()
    {
        var sut = CreateSut(BuildConfig(), chatConfigured: false);

        var status = await sut.ProbeAsync("gh", CancellationToken.None);

        status.Should().NotBeNull();
        status!.Ok.Should().BeTrue();
        status.Kind.Should().Be("repo");
        status.Category.Should().Be("service");
    }

    [Fact]
    public async Task ProbeAsync_Redis_DelegatesToInfraProbe()
    {
        var sut = CreateSut(BuildConfig(), chatConfigured: false);

        var status = await sut.ProbeAsync("redis", CancellationToken.None);

        status!.Ok.Should().BeTrue();
        status.Category.Should().Be("infra");
    }

    private static AgentSmithConfig BuildConfig() => new()
    {
        Repos = new Dictionary<string, RepoConnection>
        {
            ["gh"] = new() { Type = RepoType.GitHub, Url = "https://github.com/o/r" },
            ["loc"] = new() { Type = RepoType.Local, Path = "/tmp/repo" },
        },
        Trackers = new Dictionary<string, TrackerConnection>
        {
            ["jira"] = new() { Type = TrackerType.Jira, Url = "https://example.atlassian.net" },
        },
        Agents = new Dictionary<string, AgentConfig>
        {
            ["claude-x"] = new() { Type = "claude", Model = "claude-sonnet-4-6" },
        },
        Projects = new Dictionary<string, ResolvedProject>
        {
            ["p"] = new() { Name = "p", JiraTrigger = new JiraTriggerConfig { Secret = "shhh" } },
        },
    };

    private static ConnectionDiagnosticsService CreateSut(
        AgentSmithConfig config,
        bool chatConfigured,
        IReadOnlyDictionary<string, DateTimeOffset>? lastSeen = null)
    {
        var reachable = ConnectionProbeResult.Reachable(1);

        var jobSpawner = new Mock<IJobSpawner>();
        jobSpawner.Setup(s => s.ProbeAsync(It.IsAny<CancellationToken>())).ReturnsAsync(reachable);

        var infra = new Mock<IInfraConnectivityProbe>();
        infra.Setup(p => p.ProbeRedisAsync(It.IsAny<CancellationToken>())).ReturnsAsync(reachable);
        infra.Setup(p => p.ProbePersistenceAsync(It.IsAny<CancellationToken>())).ReturnsAsync(reachable);

        var chat = new Mock<IChatConnectivityProbe>();
        chat.SetupGet(c => c.IsSlackConfigured).Returns(chatConfigured);
        chat.SetupGet(c => c.IsTeamsConfigured).Returns(false);
        chat.Setup(c => c.ProbeSlackAsync(It.IsAny<CancellationToken>())).ReturnsAsync(reachable);

        return new ConnectionDiagnosticsService(
            config,
            new StubSourceProviderFactory(),
            new StubTicketProviderFactory(),
            new Mock<IChatClientFactory>().Object,
            jobSpawner.Object,
            infra.Object,
            chat.Object,
            new FakeTracker(lastSeen ?? new Dictionary<string, DateTimeOffset>()),
            NullLogger<ConnectionDiagnosticsService>.Instance);
    }

    private sealed class FakeTracker(IReadOnlyDictionary<string, DateTimeOffset> seen) : IWebhookDeliveryTracker
    {
        public Task RecordAsync(
            string platform, DateTimeOffset receivedAtUtc, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<IReadOnlyDictionary<string, DateTimeOffset>> GetLastSeenAsync(
            CancellationToken cancellationToken = default)
            => Task.FromResult(seen);
    }
}
