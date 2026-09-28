using AgentSmith.Contracts.Services;

namespace AgentSmith.Server.Services;

/// <summary>
/// Shapes the registered subsystem health snapshots into the /health JSON body: one entry per
/// subsystem with its state, the reason it is not up, and when that last changed. The overall
/// status is "degraded" while any subsystem is down or degraded; a disabled subsystem was
/// switched off on purpose and does not degrade the server. Pure mapping.
/// </summary>
internal static class SubsystemHealthSection
{
    public static string Status(IReadOnlyList<ISubsystemHealth> healths) =>
        healths.Any(h => h.State is SubsystemState.Down or SubsystemState.Degraded) ? "degraded" : "ok";

    public static object[] From(IReadOnlyList<ISubsystemHealth> healths) =>
        healths
            .Select(h => (object)new
            {
                name = h.Name,
                state = h.State.ToString().ToLowerInvariant(),
                reason = h.Reason,
                last_changed_utc = h.LastChangedUtc?.ToString("O"),
            })
            .ToArray();
}
