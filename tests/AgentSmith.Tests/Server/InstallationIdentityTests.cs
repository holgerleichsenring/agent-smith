using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.Diagnostics;
using AgentSmith.Server.Services.Startup;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using StackExchange.Redis;

namespace AgentSmith.Tests.Server;

/// <summary>
/// 2026-08-27-729e: what this installation is running, on one surface. Every number here
/// already existed and was readable nowhere — a version was visible exactly when it was
/// WRONG, through a mismatch finding, and never when somebody simply wanted to know it.
/// <para>
/// The Finding_ cases below assert on detectors this phase deliberately did NOT rebuild.
/// A split release is already a finding: <see cref="BuildMismatchDetector"/> reports the
/// caller's bundle against this server, and <see cref="PinnedAgentProbe"/> reports a
/// project pinned away from this release. A second opinion on the same facts would be a
/// second thing to keep true, so these pin the existing answers instead of adding one.
/// </para>
/// </summary>
public sealed class InstallationIdentityTests : IDisposable
{
    private const string Release = "1.2.3";
    private const string ServerRevision = "1111111111111111111111111111111111111111";
    private const string DashboardRevision = "2222222222222222222222222222222222222222";
    private const string Project = "alpha";

    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly MutableTimeProvider _clock = new() { Now = DateTimeOffset.UnixEpoch };
    private ServiceProvider? _scopes;

    public void Dispose()
    {
        _scopes?.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task Report_AllThreeFromOneRelease_NamesItOnce()
    {
        // The dashboard's half joins this on the SURFACE, not here: its release cannot
        // reach the server — the findings request names the caller's revision and the
        // caller's version is constructed as null on purpose — so the browser renders the
        // constant its own bundle was stamped with, labelled as its own.
        var report = await Reporter(serverRelease: Release, pinned: "").ReadAsync(default);

        report.ServerRelease.Should().Be(Release);
        report.Agents.Should().ContainSingle().Which.Version.Should().Be(Release);
        report.Agents.Single().Source.Should().Be(SandboxAgentRelease.Derived);
        Releases(report).Should().ContainSingle().Which.Should().Be(Release);
    }

    [Fact]
    public async Task Report_AServerWithNoReleaseVariable_SaysSoInsteadOfGuessing()
    {
        // The locally built server that failed late and obscurely: no AGENTSMITH_RELEASE_VERSION,
        // so the agent tag has nothing to derive from and the resolver throws.
        var report = await Reporter(serverRelease: null, pinned: "").ReadAsync(default);

        report.ServerRelease.Should().BeNull();
        report.Agents.Single().Version.Should().BeNull();
        report.Agents.Single().Source.Should().Be(SandboxAgentRelease.Underivable);
    }

    [Fact]
    public async Task Report_APinnedProject_SaysThePinIsWhereItCameFrom()
    {
        var report = await Reporter(serverRelease: Release, pinned: "1.1.0").ReadAsync(default);

        report.Agents.Single().Version.Should().Be("1.1.0");
        report.Agents.Single().Source.Should().Be(SandboxAgentRelease.Pinned);
    }

    [Fact]
    public async Task Report_ThePendingMigrationCount_IsTheOneTheProbeComputes()
    {
        // The schema is deliberately NOT migrated, so there really are migrations pending.
        var probe = RealPersistenceProbe();

        var state = await probe.ReadPersistenceStateAsync(default);
        var report = await Reporter(Release, "", probe).ReadAsync(default);
        var verdict = await probe.ProbePersistenceAsync(default);

        state.PendingMigrations.Should().BePositive("an unmigrated schema has migrations pending");
        report.Database.PendingMigrations.Should().Be(state.PendingMigrations);
        report.Database.Reachable.Should().BeTrue();
        verdict.Error.Should().Contain($"{state.PendingMigrations} pending migration",
            "the report and the reachability verdict come from ONE read, not two counts");
    }

    [Fact]
    public async Task Report_TheProvider_IsTheConfiguredOne()
    {
        var report = await Reporter(Release, "", provider: "postgresql").ReadAsync(default);

        report.Database.Provider.Should().Be("postgresql");
    }

    [Fact]
    public void Finding_ThreeComponentsDisagree_NamesEachAndItsRelease()
    {
        var server = new BuildIdentity(ServerRevision, Release);
        var detector = new BuildMismatchDetector(server, _clock);
        _clock.Now += BuildMismatchDetector.RolloutWindow * 2;

        var dashboard = detector.Compare(DashboardRevision);
        var agent = AgentFindings(pinned: "1.1.0", serverRelease: Release);

        dashboard.Should().ContainSingle().Which.Reason
            .Should().Contain(DashboardRevision[..12]).And.Contain(Release);
        agent.Should().ContainSingle().Which.Reason
            .Should().Contain("1.1.0").And.Contain(Release);
    }

    [Fact]
    public void Finding_AllThreeAgree_IsSilent()
    {
        var detector = new BuildMismatchDetector(new BuildIdentity(ServerRevision, Release), _clock);
        _clock.Now += BuildMismatchDetector.RolloutWindow * 2;

        detector.Compare(ServerRevision).Should().BeEmpty();
        AgentFindings(pinned: "", serverRelease: Release).Should().BeEmpty();
    }

    private static IReadOnlyList<StartupFinding> AgentFindings(string pinned, string? serverRelease)
    {
        var probe = new PinnedAgentProbe(ConfigWith("sqlite"), Versions(pinned, serverRelease));
        return probe.ProbeAsync(default).GetAwaiter().GetResult();
    }

    private static IReadOnlyList<string> Releases(InstallationIdentityResponse report) =>
        [.. new[] { report.ServerRelease }
            .Concat(report.Agents.Select(a => a.Version))
            .OfType<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)];

    private InstallationIdentityReporter Reporter(
        string? serverRelease,
        string pinned,
        IPersistenceStateReader? persistence = null,
        string provider = "sqlite") =>
        new(new BuildIdentity(ServerRevision, serverRelease), ConfigWith(provider),
            Versions(pinned, serverRelease), persistence ?? CurrentSchema(),
            NullLogger<InstallationIdentityReporter>.Instance);

    private static IAgentVersionResolver Versions(string pinned, string? serverRelease) =>
        new AgentVersionResolver(
            Options.Create(new SandboxGlobalConfig { AgentVersion = pinned }),
            new BuildIdentity(ServerRevision, serverRelease));

    private static AgentSmithConfig ConfigWith(string provider) => new()
    {
        Projects = new() { [Project] = new ResolvedProject { Name = Project } },
        Persistence = new PersistenceConfig { Provider = provider },
    };

    private static IPersistenceStateReader CurrentSchema()
    {
        var reader = new Mock<IPersistenceStateReader>();
        reader.Setup(r => r.ReadPersistenceStateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PersistenceState(true, 0, null));
        return reader.Object;
    }

    // The real probe over an in-memory SQLite that was never migrated — the only way to
    // show that the count the report states is the count the probe computed.
    private InfraConnectivityProbe RealPersistenceProbe()
    {
        _connection.Open();
        var services = new ServiceCollection();
        services.AddScoped(_ => new AgentSmithDbContext(
            new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options));
        _scopes = services.BuildServiceProvider();
        return new InfraConnectivityProbe(
            Mock.Of<IConnectionMultiplexer>(),
            _scopes.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<InfraConnectivityProbe>.Instance);
    }

    private sealed class MutableTimeProvider : TimeProvider
    {
        public DateTimeOffset Now { get; set; }

        public override DateTimeOffset GetUtcNow() => Now;
    }
}
