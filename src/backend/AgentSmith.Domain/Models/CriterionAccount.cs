namespace AgentSmith.Domain.Models;

/// <summary>
/// p0420: what one criterion of the ratified spec is accounted for by.
/// <para>
/// <see cref="Citation"/> is a path the diff is claimed to satisfy it at. It is checked
/// against the diff's own file list, so a criterion cannot be satisfied by a file the
/// phase never touched. That check is about INVENTION, not semantics: a real path may
/// still fail to mean what the account claims, which is the reviewer's twenty seconds.
/// </para>
/// </summary>
/// <param name="Mechanical">
/// True when the answer came from a command, not from a model — a green build is
/// evidence of a different kind and says so in the account.
/// </param>
/// <param name="Antecedent">
/// 2026-08-25-9749: on a <see cref="AccountDisposition.NotApplicable"/> row, the
/// precondition the account searched the base for and found absent. Without it a human
/// auditing the corpus cannot tell a vacuous conditional from a dodged one, and it is the
/// field an overrule is recorded against.
/// </param>
public sealed record CriterionAccount(
    string Criterion,
    AccountDisposition Disposition,
    string? Citation = null,
    string? Note = null,
    bool Mechanical = false,
    string? Antecedent = null)
{
    /// <summary>The branch satisfies it. Deliberately NOT named <c>Satisfied</c>: the bool
    /// this replaced had ten readers, and a reader that kept compiling would have kept
    /// reading "not applicable" as "not satisfied".</summary>
    public bool IsSatisfied => Disposition is AccountDisposition.Satisfied;

    /// <summary>The criterion is still open — the only disposition that is a shortfall, is
    /// handed back for repair, and fails a phase.</summary>
    public bool IsOutstanding => Disposition is AccountDisposition.NotSatisfied;

    /// <summary>The account declined to judge it, because the base disproves what it
    /// applies to.</summary>
    public bool IsNotApplicable => Disposition is AccountDisposition.NotApplicable;
}
