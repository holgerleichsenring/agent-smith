using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Core.Services.Webhooks;
using AgentSmith.Server.Contracts;
using AgentSmith.Server.Services.Diagnostics;
using AgentSmith.Server.Services.Webhooks;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
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

    // p0506: the panel's "secret configured" badge and the verifier's refusal must be the
    // same fact. They read one resolver now; before, each carried its own copy of the
    // platform-to-env-var table and the verifier ignored both when the header was absent.
    [Fact]
    public async Task Diagnostics_SecretConfigured_MatchesWhatTheVerifierRequires()
    {
        var config = BuildConfig();
        var resolver = new WebhookSecretResolver(_ => "the-shared-secret");
        var sut = CreateSut(config, chatConfigured: false, webhookSecretEnv: "the-shared-secret");
        var services = new ServiceCollection()
            .AddSingleton<IWebhookSecretResolver>(resolver)
            .AddSingleton(new ServerContext("agentsmith.yml"))
            .AddSingleton<IConfigurationLoader>(new FixedConfigurationLoader(config))
            .BuildServiceProvider();
        var verifier = new WebhookSignatureVerifier(services, NullLogger.Instance);
        var unsigned = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var snapshot = await sut.GetSnapshotAsync(CancellationToken.None);

        foreach (var webhook in snapshot.Webhooks)
        {
            webhook.SecretConfigured.Should().BeTrue();
            verifier.Validate(webhook.Platform, "{}", unsigned).Should().BeFalse(
                "{0} reports a configured secret, so an unsigned delivery must be refused",
                webhook.Platform);
        }
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
        IReadOnlyDictionary<string, DateTimeOffset>? lastSeen = null,
        string? webhookSecretEnv = null)
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
            new WebhookSecretResolver(_ => webhookSecretEnv),
            NullLogger<ConnectionDiagnosticsService>.Instance);
    }

    private sealed class FixedConfigurationLoader(AgentSmithConfig config) : IConfigurationLoader
    {
        public ConfigFileReadFact? LastRead => null;

        public AgentSmithConfig LoadConfig(string configPath) => config;
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
