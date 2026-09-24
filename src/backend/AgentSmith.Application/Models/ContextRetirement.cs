namespace AgentSmith.Application.Models;

/// <summary>
/// 2026-09-23-4711: what one repository's retirement pass did — the contexts moved aside,
/// and the ones the sandbox refused to move.
/// <para>
/// The refusals are carried rather than swallowed: a context that could not be moved is
/// still declared beside the ones that replaced it, which is the state this exists to end.
/// They do not FAIL the run — the rounds have already written, and throwing the derivation
/// away over a move is a worse answer than a pull request that says which one did not.
/// </para>
/// </summary>
/// <param name="Retired">Context names whose directory was moved aside, in tree order.</param>
/// <param name="Refused">Context names the move failed for, each with the sandbox's reason.</param>
public sealed record ContextRetirement(
    IReadOnlyList<string> Retired, IReadOnlyList<string> Refused)
{
    /// <summary>A pass that found nothing to retire.</summary>
    public static readonly ContextRetirement None = new([], []);
}
