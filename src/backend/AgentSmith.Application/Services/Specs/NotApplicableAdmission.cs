namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-08-25-9749: what a claim of NOT APPLICABLE has to bring before it is admitted.
/// <para>
/// The judgement itself — whether an antecedent SHOULD be disprovable — is not machine
/// checkable. No check can tell "every host that already configures X" apart from a
/// prohibition, or from a default with no token to find, and one live run produced three
/// wrong licences on its own. So this enforces only what CAN be enforced: the row cites a
/// search of the BASE that ran and found nothing, and names the antecedent it takes to be
/// false. The judgement is left to the labelled corpus to MEASURE. An unenforceable rule
/// dressed as a check is worse than a stated limit.
/// </para>
/// <para>
/// A claim that fails any of these does not fail the account — it falls back to not
/// satisfied, which is the answer it would have had before this existed.
/// </para>
/// </summary>
internal sealed class NotApplicableAdmission(IReadOnlyList<string> baseAbsences)
{
    /// <summary>Why this claim is not admitted, or null when it is.</summary>
    public string? Refusal(AccountRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        if (baseAbsences.Count == 0)
            return "reported not applicable, but no search of the base found anything absent, "
                + "so nothing here shows the precondition was never there";
        if (string.IsNullOrWhiteSpace(row.Antecedent))
            return "reported not applicable without naming the precondition it takes to be false";
        return Proof(row) is null
            ? "reported not applicable without citing a search of the BASE that ran and found "
              + "nothing — a search of the branch says what is there now, not what was there before"
            : null;
    }

    /// <summary>
    /// The base search the row rests on, read by the same citation grammar every other piece
    /// of evidence is read by. The EVIDENCE LINE is returned rather than the pattern the row
    /// wrote, because the line names the ref that was read — and "not applicable over
    /// origin/main" and "not applicable over the branch itself" are the two answers a reader
    /// has to be able to tell apart.
    /// </summary>
    public string? Proof(AccountRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        return baseAbsences.FirstOrDefault(
            line => row.Cited.Any(citation => CitationMatch.Names(line, citation)));
    }
}
