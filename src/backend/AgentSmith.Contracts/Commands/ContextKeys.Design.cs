namespace AgentSmith.Contracts.Commands;

public static partial class ContextKeys
{
    /// <summary>
    /// 2026-10-01-283dg: IReadOnlyList&lt;ContextDocument&gt; — the root DESIGN.md of each
    /// repository that carries one, one document per repository. A run's LoadDesignSystem and a
    /// design turn's GroundSpecDialog publish it; the master renders it beside the template
    /// section. Absent when no repository in scope carries the file.
    /// </summary>
    public const string DesignSystem = "DesignSystem";

    /// <summary>
    /// 2026-11-10-fcd7b: bool — the design turn's poster holds runs.control. Seeded true only then; a turn without
    /// it records no project memory (and, with 2026-11-10-4778, runs nothing).
    /// </summary>
    public const string MayStartRuns = "MayStartRuns";
}
