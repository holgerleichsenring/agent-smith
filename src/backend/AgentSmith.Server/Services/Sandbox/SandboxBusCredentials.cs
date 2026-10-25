using System.Collections.Concurrent;
using System.Security.Cryptography;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Sandbox.Wire;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace AgentSmith.Server.Services.Sandbox;

/// <summary>
/// 2026-10-25-c5a7e: issues each sandbox its own Redis ACL user, limited to its job's keys
/// (<see cref="SandboxBusAcl"/>), and deletes it when the sandbox goes. ACL users are not in
/// the AOF, so the users of live sandboxes are re-issued when the connection comes back.
/// A Redis that refuses ACL SETUSER falls back to the shared credential with a warning
/// finding (operator decision): runs keep working, and the loss of isolation is visible.
/// </summary>
public sealed class SandboxBusCredentials
{
    private const string Acl = "ACL";
    private readonly ConcurrentDictionary<string, string> _live = new(StringComparer.Ordinal);
    private readonly IConnectionMultiplexer _redis;
    private readonly bool _isShared;
    private readonly IStartupFindings _findings;
    private readonly ILogger<SandboxBusCredentials> _logger;

    public SandboxBusCredentials(IConnectionMultiplexer redis, SandboxBusCredentialSetting setting,
        IStartupFindings findings, ILogger<SandboxBusCredentials> logger)
    {
        (_redis, _isShared, _findings, _logger) = (redis, setting.IsShared, findings, logger);
        if (!_isShared) redis.ConnectionRestored += OnRestored;
    }

    public async Task<SandboxBusCredential> IssueAsync(string jobId, string serverUrl)
    {
        if (_isShared) return SandboxBusCredential.Shared(serverUrl);
        var password = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        if (!await TrySetUserAsync(jobId, password)) return SandboxBusCredential.Shared(serverUrl);
        _live[jobId] = password;
        var url = SandboxBusUrl.Compose(serverUrl, SandboxBusAcl.UserName(jobId), password);
        return new SandboxBusCredential(url, () => DeleteAsync(jobId));
    }

    /// <summary>Deletes the job's user; the reapers call it for a sandbox they remove.</summary>
    public async Task DeleteAsync(string jobId)
    {
        if (_isShared || string.IsNullOrEmpty(jobId)) return;
        _live.TryRemove(jobId, out _);
        try
        {
            await _redis.GetDatabase().ExecuteAsync(Acl, "DELUSER", SandboxBusAcl.UserName(jobId));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not delete the Redis user of sandbox job {JobId}", jobId);
        }
    }

    /// <summary>Re-issues the users of every live sandbox; runs on ConnectionRestored.</summary>
    public async Task ReissueAsync()
    {
        foreach (var (jobId, password) in _live)
        {
            try { await TrySetUserAsync(jobId, password); }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not re-issue the Redis user of sandbox job {JobId}", jobId);
            }
        }
    }

    private void OnRestored(object? sender, ConnectionFailedEventArgs e) => _ = ReissueAsync();

    private async Task<bool> TrySetUserAsync(string jobId, string password)
    {
        object[] args = ["SETUSER", SandboxBusAcl.UserName(jobId), .. SandboxBusAcl.Rules(jobId, password)];
        try
        {
            await _redis.GetDatabase().ExecuteAsync(Acl, args);
            return true;
        }
        catch (RedisServerException ex)
        {
            _logger.LogWarning(ex, "Redis refused ACL SETUSER; sandbox job {JobId} gets the shared credential", jobId);
            _findings.Record(Refused(ex.Message));
            return false;
        }
    }

    internal static StartupFinding Refused(string reply) => new(
        StartupSubsystems.Redis,
        StartupFindingSeverity.Advisory,
        $"Redis refused ACL SETUSER ({reply}), so sandboxes receive the server's shared Redis "
        + "credential: a command in one sandbox can read and forge another job's steps. Give the "
        + "server's Redis user ACL rights, or set "
        + $"{SandboxBusCredentialModeReader.Variable}={SandboxBusCredentialModeReader.SharedValue} "
        + "to acknowledge the shared credential.",
        Field: SandboxBusCredentialModeReader.Variable);
}
