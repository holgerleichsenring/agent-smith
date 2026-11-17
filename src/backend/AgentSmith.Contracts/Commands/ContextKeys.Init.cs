namespace AgentSmith.Contracts.Commands;

/// <summary>
/// p0490: context keys owned by the init-project pipeline's tail.
/// </summary>
public static partial class ContextKeys
{
    /// <summary>2026-10-04-2bf2: bool riding the LAUNCH request — the operator ticked
    /// "Refresh principles" on this init, so an existing principles.md is recomposed from
    /// the catalog (core, language delta, framework overlays) and its Project Specifics
    /// section is carried over verbatim. Per launch: a project setting would refresh on every later init nobody meant to refresh.
    /// Absent (or false) preserves the file. Read it with <c>PipelineContext.Flag</c>.</summary>
    public const string RefreshPrinciples = "RefreshPrinciples";
}
