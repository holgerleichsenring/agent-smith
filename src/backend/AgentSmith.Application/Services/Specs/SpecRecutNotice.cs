using AgentSmith.Contracts.Specs;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// The line in the derivation-time comment that tells an author their input took: the
/// ticket edit (2026-09-08-5cd2) or the comment (2026-09-08-4aa9) that re-cut the
/// unstarted tail, the revision it postdates, and which phases stayed because they
/// already ran. The author learns it from the ticket, without opening the branch. Any
/// other revision renders nothing.
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
            _ => (null, null),
        };
        if (since is null) return string.Empty;
        return $"{since} revision {set.Current.Number - 1} was cut: {Kept(set)} {source}.\n\n";
    }

    private static string Kept(SpecSet set) =>
        set.Executed.Count == 0
            ? "no phase had run yet, so the whole set was cut again"
            : $"{string.Join(", ", set.Executed)} already ran and stayed as it was; the rest was cut again";
}
