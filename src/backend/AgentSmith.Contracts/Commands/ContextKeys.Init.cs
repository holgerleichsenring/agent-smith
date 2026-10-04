namespace AgentSmith.Contracts.Commands;

/// <summary>
/// p0490: context keys owned by the init-project pipeline's tail.
/// </summary>
public static partial class ContextKeys
{
    /// <summary>p0490: bool riding the LAUNCH request — the operator ticked auto-accept
    /// on the init they started, so this run may finish the pull requests it opens. It
    /// is deliberately not project configuration: consent belongs to the click that
    /// started THIS run, not to whatever opens a pull request next. Absent (or false)
    /// means every pull request stays open. Arrives as a JsonElement when the request
    /// came through the Redis job queue, so read it with <c>PipelineContext.Flag</c>.</summary>
    public const string AutoCompletePullRequests = "AutoCompletePullRequests";

    /// <summary>2026-10-04-2bf2: bool riding the LAUNCH request — the operator ticked
    /// "Refresh principles" on this init, so an existing principles.md is recomposed from
    /// the catalog (core, language delta, framework overlays) and its Project Specifics
    /// section is carried over verbatim. Per launch, like auto-accept and independent of
    /// it: a project setting would refresh on every later init nobody meant to refresh.
    /// Absent (or false) preserves the file. Read it with <c>PipelineContext.Flag</c>.</summary>
    public const string RefreshPrinciples = "RefreshPrinciples";
}
