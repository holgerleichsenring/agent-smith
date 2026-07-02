using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Server.Services.Diagnostics;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Services.Diagnostics;

/// <summary>
/// p0292: ConnectionDiagnosticsService enumerates configured repos + trackers,
/// skips Local (no remote), and builds the webhook panel from the delivery
/// tracker + configured secrets — without leaking any secret value.
/// </summary>
public sealed class ConnectionDiagnosticsServiceTests
{
    [Fact]
    public async Task GetSnapshotAsync_ListsReposAndTrackers_SkipsLocal()
    {
        var sut = CreateSut(BuildConfig(), new FakeTracker(new Dictionary<string, DateTimeOffset>()));

        var snapshot = await sut.GetSnapshotAsync(CancellationToken.None);

        snapshot.Connections.Select(c => c.Name).Should().BeEquivalentTo("gh", "jira");
        snapshot.Connections.Single(c => c.Name == "gh").Kind.Should().Be("repo");
        snapshot.Connections.Single(c => c.Name == "jira").Kind.Should().Be("tracker");
    }

    [Fact]
    public async Task GetSnapshotAsync_JiraProjectSecret_ReportsSecretConfiguredAndLastSeen()
    {
        var seen = new Dictionary<string, DateTimeOffset> { ["jira"] = DateTimeOffset.UnixEpoch };
        var sut = CreateSut(BuildConfig(), new FakeTracker(seen));

        var snapshot = await sut.GetSnapshotAsync(CancellationToken.None);

        var jira = snapshot.Webhooks.Single(w => w.Platform == "jira");
        jira.SecretConfigured.Should().BeTrue();
        jira.LastReceivedUtc.Should().Be(DateTimeOffset.UnixEpoch);
        snapshot.Webhooks.Single(w => w.Platform == "gitlab").LastReceivedUtc.Should().BeNull();
    }

    [Fact]
    public async Task ProbeAsync_KnownConnection_ReturnsOkStatusFromProvider()
    {
        var sut = CreateSut(BuildConfig(), new FakeTracker(new Dictionary<string, DateTimeOffset>()));

        var status = await sut.ProbeAsync("gh", CancellationToken.None);

        status.Should().NotBeNull();
        status!.Ok.Should().BeTrue();
        status.Kind.Should().Be("repo");
    }

    [Fact]
    public async Task ProbeAsync_UnknownName_ReturnsNull()
    {
        var sut = CreateSut(BuildConfig(), new FakeTracker(new Dictionary<string, DateTimeOffset>()));

        (await sut.ProbeAsync("does-not-exist", CancellationToken.None)).Should().BeNull();
    }

    [Fact]
    public async Task ProbeAsync_KnownName_ReturnsThatConnectionOnly()
    {
        var sut = CreateSut(BuildConfig(), new FakeTracker(new Dictionary<string, DateTimeOffset>()));

        var status = await sut.ProbeAsync("jira", CancellationToken.None);

        status.Should().NotBeNull();
        status!.Name.Should().Be("jira");
        status.Kind.Should().Be("tracker");
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
        Projects = new Dictionary<string, ResolvedProject>
        {
            ["p"] = new() { Name = "p", JiraTrigger = new JiraTriggerConfig { Secret = "shhh" } },
        },
    };

    private static ConnectionDiagnosticsService CreateSut(
        AgentSmithConfig config, IWebhookDeliveryTracker tracker) =>
        new(config,
            new StubSourceProviderFactory(),
            new StubTicketProviderFactory(),
            tracker,
            NullLogger<ConnectionDiagnosticsService>.Instance);

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
