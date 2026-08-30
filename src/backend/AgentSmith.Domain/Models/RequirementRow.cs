namespace AgentSmith.Domain.Models;

/// <summary>
/// 2026-08-30-3c12: one entry of the standard at one station of one entry group, AFTER the
/// run settled what the scan said about it.
/// <para>
/// <paramref name="Disposition"/> is not the model's word for it. A verdict counts only
/// when its citation lands in the files this run really read — the same rule that decides
/// whether a finding may call itself analyzed-from-source — so an answer citing the file
/// next door settles as <see cref="RequirementDisposition.Unanswered"/> and
/// <paramref name="Note"/> says which case it is.
/// </para>
/// </summary>
public sealed record RequirementRow(
    VerificationStation Station,
    RequirementOperation Operation,
    string RequirementId,
    string Level,
    string Text,
    RequirementDisposition Disposition,
    RequirementScope Scope,
    string Citation,
    string Note)
{
    /// <summary>Whether the scan produced a verdict this row can stand on.</summary>
    public bool Answered => Disposition is RequirementDisposition.Met
        or RequirementDisposition.Unmet or RequirementDisposition.CannotAnswer;

    /// <summary>The entry as a reader cites it: the id together with the level it sits at.</summary>
    public string Reference => $"{RequirementId} (L{Level})";
}
