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
}
