namespace AgentSmith.Contracts.Runs;

/// <summary>
/// p0341h: what a run spent its time ON, at the two levels a reader actually asks about —
/// which pipeline steps it ran, and which sandbox commands those steps issued.
/// <para>
/// The panel this replaces was a column of totals, and a column of totals answers "how
/// much" without ever answering "on what". Worse, one of its numbers was a lie by
/// construction: it folded <c>ToolResultEvent</c>, which the trail never receives — the
/// agent's file reads and greps are recorded as SANDBOX COMMANDS, so the panel reported
/// zero tool calls for a run that issued 444.
/// </para>
/// <para>
/// Ordered by DURATION, not by count: twelve builds outweigh a hundred greps, and the
/// ordering is the finding.
/// </para>
/// </summary>
public sealed record RunWorkBreakdown(
    IReadOnlyList<RunWorkKind> Pipeline,
    IReadOnlyList<RunWorkKind> Sandbox)
{
    public static RunWorkBreakdown Empty { get; } = new([], []);
}

/// <summary>
/// One kind of work, folded: how often it ran and how long that took in total.
/// <c>Failed</c> is carried for sandbox commands, where a non-zero exit is the thing a
/// reader scans for; it stays 0 for pipeline rows, which report their outcome elsewhere.
/// </summary>
public sealed record RunWorkKind(string Label, int Count, long DurationMs, int Failed = 0);
