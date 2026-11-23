namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// 2026-11-23-31dci: how long a ticket may wait on nobody — for its model place, for sandbox capacity, for the restart
/// after a stopped server — before its hold becomes an admin's, from agentsmith.yml's top-level <c>holds:</c> block. A
/// wait that long means something outside the run is wrong. Read when a hold is read; nothing runs in the background.
/// </summary>
public sealed class HoldsConfig
{
    /// <summary>The key the admin hold's sentence names.</summary>
    public const string EscalateAfterMinutesKey = "holds.escalate_after_minutes";

    /// <summary>The minutes a nobody hold's wait may last before it is reported to the admin; 0 never reports one.</summary>
    public int EscalateAfterMinutes { get; set; } = 60;

    /// <summary>What is wrong with these values, one sentence each; empty when nothing is. The ONE rule — the load and
    /// the settings store refuse what this names, never clamping it.</summary>
    public IReadOnlyList<string> Problems() =>
        EscalateAfterMinutes >= 0
            ? []
            : [$"{EscalateAfterMinutesKey} is {EscalateAfterMinutes}; it must be 0 (never report a wait to the admin) or more, "
               + "because it counts the minutes a ticket may wait on nobody before a wait that long is reported as "
               + "something outside the run gone wrong"];
}
