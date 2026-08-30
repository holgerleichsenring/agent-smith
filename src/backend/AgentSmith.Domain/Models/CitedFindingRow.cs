namespace AgentSmith.Domain.Models;

/// <summary>
/// 2026-08-30-03e1: one cited finding AFTER the run settled its evidence.
/// <para>
/// <paramref name="Located"/> is not the model's word for it. A citation counts only when
/// it lands in the files this run really read — the same rule that decides whether a
/// finding may call itself analyzed-from-source and the rule the entry map settles a
/// station with — so a finding placed in the file next door settles unlocated and
/// <paramref name="Note"/> says which case it is.
/// </para>
/// </summary>
public sealed record CitedFindingRow(
    VerificationStation Station,
    string RequirementId,
    string Level,
    string Text,
    string Detail,
    bool Located,
    string Citation,
    string Note)
{
    /// <summary>The entry as a reader cites it: the id together with the level it sits at.</summary>
    public string Reference => $"{RequirementId} (L{Level})";
}
