using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Sandbox;

/// <summary>
/// 2026-09-22-2d11a: resolves the sandbox hold window for ONE scan — the project's own
/// value, then the process-wide sandbox block, then <see cref="EnvironmentVariable"/>,
/// then <see cref="DefaultWindow"/>. Zero holds nothing, which is the behaviour of
/// every deployment that never sets it.
/// <para>
/// A resolution that THREW keeps the last one this process made and the scan reaps
/// nothing new: turning an operator's deliberate zero into the built-in default would
/// hold sandboxes they asked not to be held, and the read costs a catalog assembly
/// against the document store, so it happens once per scan rather than once per
/// container.
/// </para>
/// </summary>
public sealed class SandboxHoldWindowResolver(
    IConfigurationLoader configLoader,
    ServerContext serverContext,
    ILogger<SandboxHoldWindowResolver> logger)
{
    /// <summary>The empty-store fallback: read only when the catalog names no window.</summary>
    public const string EnvironmentVariable = "SANDBOX_HOLD_SECONDS";

    /// <summary>
    /// Three minutes: long enough to cover reading a reply and typing the next question,
    /// which is where a design conversation lives. A wrong value costs idle resources and
    /// never a refused run, because a hold is released before any capacity probe.
    /// </summary>
    public static readonly TimeSpan DefaultWindow = TimeSpan.FromSeconds(180);

    private SandboxHoldWindows? _lastResolved;

    /// <summary>
    /// The windows this scan decides with, or null when no scan has ever resolved one —
    /// in which case the caller spares everything it cannot judge.
    /// </summary>
    public SandboxHoldWindows? ResolveForScan()
    {
        try
        {
            var config = configLoader.LoadConfig(serverContext.ConfigPath);
            _lastResolved = new SandboxHoldWindows(ProcessWide(config.Sandbox), ByProject(config));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "The sandbox hold window could not be resolved — keeping the last resolution ({Window})",
                _lastResolved?.ProcessWide);
        }
        return _lastResolved;
    }

    private static TimeSpan ProcessWide(SandboxGlobalConfig sandbox)
    {
        if (sandbox.HoldSeconds is { } configured) return TimeSpan.FromSeconds(Math.Max(0, configured));
        return int.TryParse(Environment.GetEnvironmentVariable(EnvironmentVariable), out var fromEnvironment)
            ? TimeSpan.FromSeconds(Math.Max(0, fromEnvironment))
            : DefaultWindow;
    }

    private static IReadOnlyDictionary<string, TimeSpan> ByProject(AgentSmithConfig config) =>
        config.Projects
            .Where(entry => entry.Value.Sandbox?.HoldSeconds is not null)
            .ToDictionary(
                entry => entry.Key,
                entry => TimeSpan.FromSeconds(Math.Max(0, entry.Value.Sandbox!.HoldSeconds!.Value)),
                StringComparer.OrdinalIgnoreCase);
}
