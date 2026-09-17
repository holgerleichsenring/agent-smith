namespace AgentSmith.Contracts.Specs;

/// <summary>
/// 2026-09-17-0e79a: the moment a person approved a spec set in the design conversation,
/// with where that approval came from.
/// <para>
/// <see cref="At"/> is the APPROVAL INSTANT — the one field the precedence compares. A set
/// can be approved again (2026-09-17-0e79b's amendment), so an approval is a version marker
/// and not a one-shot hand-off: the run prefers the branch artifact UNLESS a record carries
/// a newer instant than the one <c>set.yaml</c> was published from.
/// </para>
/// <para>
/// The instant and the time are ONE field on purpose. Two timestamps written together are
/// two chances to disagree, and nothing reads a "recorded at" that is not the approval.
/// </para>
/// </summary>
/// <param name="At">The approval instant — what "newer" means.</param>
/// <param name="Conversation">The design-conversation session the approval was given in,
/// so 2026-09-17-0e79b can re-open exactly that conversation to amend the set.</param>
/// <param name="Principal">Who approved it.</param>
public sealed record SpecApproval(DateTimeOffset At, string Conversation, string Principal)
{
    /// <summary>True when <paramref name="other"/> is an approval this one post-dates.</summary>
    public bool IsNewerThan(SpecApproval? other) => other is null || At > other.At;
}
