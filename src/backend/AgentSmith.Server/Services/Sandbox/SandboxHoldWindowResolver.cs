using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Sandbox;

/// <summary>
/// 2026-09-22-2d11a: resolves the sandbox hold window for ONE scan — the project's own
/// value, then the process-wide sandbox block, then
/// <see cref="SandboxHoldWindow.EnvironmentVariable"/>, then
/// <see cref="SandboxHoldWindow.Default"/>. Zero holds nothing, which is the behaviour of
/// every deployment that never sets it.
/// <para>
/// 2026-09-23-2446: the process-wide half is <see cref="SandboxHoldWindow"/>'s, because the
/// project form's inherited-value projection has to name the window THIS resolver will use
/// and one algorithm over two inputs would still be two answers.
/// </para>
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

    private static TimeSpan ProcessWide(SandboxGlobalConfig sandbox) =>
        SandboxHoldWindow.Window(SandboxHoldWindow.ProcessWide(sandbox).Value);

    private static IReadOnlyDictionary<string, TimeSpan> ByProject(AgentSmithConfig config) =>
        config.Projects
            .Where(entry => entry.Value.Sandbox?.HoldSeconds is not null)
            .ToDictionary(
                entry => entry.Key,
                entry => SandboxHoldWindow.Window(entry.Value.Sandbox!.HoldSeconds!.Value),
                StringComparer.OrdinalIgnoreCase);
}
