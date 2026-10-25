using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Server.Contracts;
using AgentSmith.Server.Services.Sandbox;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace AgentSmith.Server.Services.Startup;

/// <summary>
/// 2026-10-25-c5a7e: says when the per-job sandbox credential protects nothing. A default
/// user flagged nopass lets a sandbox connect as default and reach every key; a Redis that
/// refuses ACL commands makes every sandbox fall back to the shared credential; and the
/// shared mode itself is named, so the choice stays visible.
/// </summary>
public sealed class SandboxBusCredentialProbe(
    IConnectionMultiplexer redis,
    SandboxBusCredentialSetting setting,
    ILogger<SandboxBusCredentialProbe> logger) : IStartupProbe
{
    public string Subsystem => StartupSubsystems.Redis;

    public async Task<IReadOnlyList<StartupFinding>> ProbeAsync(CancellationToken cancellationToken)
    {
        if (setting.IsShared) return [Finding(SharedReason)];
        try
        {
            var user = await redis.GetDatabase().ExecuteAsync("ACL", "GETUSER", "default");
            return HasNoPassword(user) ? [Finding(NoPasswordReason)] : [];
        }
        catch (RedisServerException ex)
        {
            return [SandboxBusCredentials.Refused(ex.Message)];
        }
        catch (RedisException ex)
        {
            // Redis being down is RedisProbe's finding, not this one's.
            logger.LogDebug(ex, "Sandbox bus credential probe could not reach Redis");
            return [];
        }
    }

    private static bool HasNoPassword(RedisResult? user)
    {
        if (user is null || user.IsNull || user.Resp2Type != ResultType.Array) return false;
        var fields = user.ToDictionary();
        return fields.TryGetValue("flags", out var flags)
            && ((string?[]?)flags ?? []).Contains("nopass", StringComparer.Ordinal);
    }

    private static StartupFinding Finding(string reason) => new(
        StartupSubsystems.Redis, StartupFindingSeverity.Advisory, reason,
        Field: SandboxBusCredentialModeReader.Variable);

    private const string NoPasswordReason =
        "Redis's default user needs no password, so the per-job sandbox users protect nothing: a "
        + "command in a sandbox can connect as the default user and reach every job's keys. "
        + "Require a password (docs/reference/host-it/redis-password.md).";

    private const string SharedReason =
        $"{SandboxBusCredentialModeReader.Variable}={SandboxBusCredentialModeReader.SharedValue}: "
        + "every sandbox receives the server's Redis credential, so a command in one sandbox can "
        + "read and forge another job's steps.";
}
