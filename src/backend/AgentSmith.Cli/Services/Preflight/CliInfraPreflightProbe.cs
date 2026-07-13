using System.Diagnostics;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace AgentSmith.Cli.Services.Preflight;

/// <summary>
/// p0324: infra probe for the one-shot CLI composition. Redis is probed only when
/// REDIS_URL is set (interactive CLI runs never touch Redis; a set REDIS_URL means
/// the operator points doctor at a server deployment). The relational DB is
/// server-owned — the server startup preflight probes it — so it skips here with
/// that reason instead of failing laptop users who never run a server.
/// </summary>
internal sealed class CliInfraPreflightProbe(ILogger<CliInfraPreflightProbe> logger) : IPreflightInfraProbe
{
    private const string RedisUrlVariable = "REDIS_URL";

    public string? RedisUnavailableReason =>
        string.IsNullOrEmpty(Environment.GetEnvironmentVariable(RedisUrlVariable))
            ? "REDIS_URL not set — one-shot CLI runs don't use Redis; the server startup preflight probes it"
            : null;

    public string? PersistenceUnavailableReason =>
        "relational persistence is server-owned; the server startup preflight probes it "
        + "(run 'agentsmith database migrate' before server start)";

    public async Task<ConnectionProbeResult> ProbeRedisAsync(CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var options = ConfigurationOptions.Parse(
                Environment.GetEnvironmentVariable(RedisUrlVariable)!);
            options.ConnectTimeout = 5000;
            options.ConnectRetry = 1;
            await using var multiplexer = await ConnectionMultiplexer.ConnectAsync(options);
            await multiplexer.GetDatabase().PingAsync();
            return ConnectionProbeResult.Reachable(stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "CLI Redis preflight probe failed");
            return ConnectionProbeResult.Unreachable(stopwatch.ElapsedMilliseconds, ex.Message);
        }
    }

    public Task<ConnectionProbeResult> ProbePersistenceAsync(CancellationToken cancellationToken) =>
        Task.FromResult(ConnectionProbeResult.Unreachable(0, PersistenceUnavailableReason!));
}
