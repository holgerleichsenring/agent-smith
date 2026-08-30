namespace AgentSmith.Domain.Models;

/// <summary>
/// 2026-08-30-3c12: whether an answer is about reading state or about changing it.
/// <para>
/// A reviewer who follows only the read path never sees the state-changing action on
/// another actor's object, and the sharpest signal a scan can produce is the asymmetry:
/// the same resource scoped on read and unscoped on write. The two are therefore
/// enumerated apart and never averaged into one verdict per station.
/// </para>
/// </summary>
public enum RequirementOperation
{
    /// <summary>The operations that return state without changing it.</summary>
    Read,

    /// <summary>The operations that change state, produce side effects or emit output.</summary>
    Write
}
