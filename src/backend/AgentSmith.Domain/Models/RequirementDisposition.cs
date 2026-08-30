namespace AgentSmith.Domain.Models;

/// <summary>
/// 2026-08-30-3c12: what a scan settled about one entry of the verification standard at
/// one station of one entry group.
/// <para>
/// The four values that are not <see cref="Met"/> are four different facts, and collapsing
/// any two of them lets a thin run read as a thorough one. <see cref="NotAttempted"/> is a
/// BUDGET fact — the run never reached this entry group; <see cref="CannotAnswer"/> is a
/// KNOWLEDGE fact naming the input the scan lacked; <see cref="Unanswered"/> is silence,
/// which includes a stated answer whose citation resolved against nothing.
/// </para>
/// </summary>
public enum RequirementDisposition
{
    /// <summary>The scan states the entry is satisfied here, and cites where.</summary>
    Met,

    /// <summary>The scan states the entry is NOT satisfied here, and cites where it looked.</summary>
    Unmet,

    /// <summary>The scan cannot decide, and names the input it would have needed.</summary>
    CannotAnswer,

    /// <summary>An attempted group left this entry without an answer that counts.</summary>
    Unanswered,

    /// <summary>The run never reached this entry group: it lies beyond the declared cap.</summary>
    NotAttempted
}
