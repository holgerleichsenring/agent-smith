using AgentSmith.Application.Services.Preflight.Checks;
using AgentSmith.Contracts.Models.Preflight;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using FluentAssertions;

namespace AgentSmith.Tests.Services.Preflight;

/// <summary>
/// p0324: infra fails loud on an unreachable Redis (the p0238 reaper-meltdown class)
/// or a DB with pending migrations, and skips honestly where the composition cannot
/// probe (one-shot CLI).
/// </summary>
public sealed class InfraCheckTests
{
    [Fact]
    public async Task RunAsync_RedisDown_FailsWithReaperWarning()
    {
        var check = new InfraCheck(new ScriptedInfraProbe(
            redisResult: ConnectionProbeResult.Unreachable(5000, "connection refused (localhost:6379)"),
            persistenceResult: ConnectionProbeResult.Reachable(3)));

        var result = await check.RunAsync(CancellationToken.None);

        result.Status.Should().Be(PreflightStatus.Fail);
        result.Message.Should().Contain("connection refused");
        result.FixHint.Should().Contain("REDIS_URL").And.Contain("p0238");
    }

    [Fact]
    public async Task RunAsync_PendingMigrations_Fails()
    {
        var check = new InfraCheck(new ScriptedInfraProbe(
            redisResult: ConnectionProbeResult.Reachable(1),
            persistenceResult: ConnectionProbeResult.Unreachable(9, "2 pending migration(s)")));

        var result = await check.RunAsync(CancellationToken.None);

        result.Status.Should().Be(PreflightStatus.Fail);
        result.Message.Should().Contain("pending migration");
        result.FixHint.Should().Contain("database migrate");
    }

    [Fact]
    public async Task RunAsync_NothingProbeableHere_SkipsWithReasons()
    {
        var check = new InfraCheck(new ScriptedInfraProbe(
            redisReason: "REDIS_URL not set",
            persistenceReason: "server-owned"));

        var result = await check.RunAsync(CancellationToken.None);

        result.Status.Should().Be(PreflightStatus.Skip);
        result.Message.Should().Contain("REDIS_URL not set").And.Contain("server-owned");
    }

    [Fact]
    public async Task RunAsync_BothReachable_Passes()
    {
        var check = new InfraCheck(new ScriptedInfraProbe(
            redisResult: ConnectionProbeResult.Reachable(1),
            persistenceResult: ConnectionProbeResult.Reachable(2)));

        var result = await check.RunAsync(CancellationToken.None);

        result.Status.Should().Be(PreflightStatus.Pass);
    }

    private sealed class ScriptedInfraProbe(
        ConnectionProbeResult? redisResult = null,
        ConnectionProbeResult? persistenceResult = null,
        string? redisReason = null,
        string? persistenceReason = null) : IPreflightInfraProbe
    {
        public string? RedisUnavailableReason => redisReason;

        public string? PersistenceUnavailableReason => persistenceReason;

        public Task<ConnectionProbeResult> ProbeRedisAsync(CancellationToken cancellationToken) =>
            Task.FromResult(redisResult!);

        public Task<ConnectionProbeResult> ProbePersistenceAsync(CancellationToken cancellationToken) =>
            Task.FromResult(persistenceResult!);
    }
}
