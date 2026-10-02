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
}
