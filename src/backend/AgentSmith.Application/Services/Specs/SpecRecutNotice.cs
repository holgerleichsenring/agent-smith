using AgentSmith.Contracts.Specs;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// The line in the derivation-time comment that tells an author their input took: the
/// ticket edit (2026-09-08-5cd2) or the comment (2026-09-08-4aa9) that re-cut the
/// unstarted tail, the revision it postdates, and which phases stayed because they
/// already ran. The author learns it from the ticket, without opening the branch. Any
/// other revision renders nothing.
/// <para>
/// 2026-09-22-8b25: a DEMAND renders one too — it is the only input that re-cuts a set somebody
/// approved, so it is the revision whose reader most needs to be told which phases survived it.
/// The "already ran and stayed as it was" half is not a promise the model is asked to keep:
/// <see cref="SpecDerivationParser"/> re-uses the executed head verbatim and starts the model's
/// phases after it, so a re-cut cannot reach one whatever the reply says.
/// </para>
/// </summary>
public static class SpecRecutNotice
{
    public static string Render(SpecSet set)
    {
        ArgumentNullException.ThrowIfNull(set);
        var (since, source) = set.Current.Cause switch
        {
            SpecRevisionCause.TicketEdit => ("The ticket text changed since", "from the current text"),
            SpecRevisionCause.Comment => ("The ticket was commented on after", "with the comment in view"),
            SpecRevisionCause.RecutDemand =>
                ("A re-cut was demanded on the ticket after", "from the ticket as it now reads"),
            SpecRevisionCause.Rework =>
                ("Changes were requested on the pull request after", "with the review in view"),
            SpecRevisionCause.StatusBack =>
                ("The ticket was moved back to work after", "with the feedback since in view"),
            _ => (null, null),
        };
        if (since is null) return string.Empty;
        // 2026-10-08-f114: at the cap the model did not run, so nothing was cut again or added.
        if (set.UnexecutedTail.Count == 0 && set.Executed.Count >= SpecSet.MaxPhases)
            return $"{since} revision {set.Current.Number - 1} was cut, but all {SpecSet.MaxPhases} phases of this set "
                + "have run — the most one set can hold — so nothing was added. File the feedback as a new ticket.\n\n";
        return $"{since} revision {set.Current.Number - 1} was cut: {Kept(set)} {source}.\n\n";
    }

    private static string Kept(SpecSet set) =>
        set.Executed.Count == 0
            ? "no phase had run yet, so the whole set was cut again"
            : set.UnexecutedTail.Count > 0
                ? $"{string.Join(", ", set.Executed)} already ran and stayed as it was; the rest was cut again"
                : $"{string.Join(", ", set.Executed)} already ran and stayed as it was";
}
