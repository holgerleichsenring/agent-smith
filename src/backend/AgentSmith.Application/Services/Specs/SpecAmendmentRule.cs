using AgentSmith.Contracts.Specs;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// Whether an input the run saw re-cuts the set the branch already carries.
/// <para>
/// A comment or an edited ticket is input the model has not seen, whatever the branch carries —
/// amend the SAME set rather than produce a fresh reading of the prose. A reviewer's edit is
/// already the correction and needs no model at all. A bare re-trigger re-cuts a set nothing has
/// run yet (the previous run ended before the cut was worked, and a fallback cut is not meant to
/// be permanent) but continues a set in flight: an executed head is work on the branch, and the
/// inputs that re-cut its tail are read as their own causes.
/// </para>
/// <para>
/// 2026-09-17-0e79b: an APPROVED set is never re-cut, for ANY of those causes. 2026-09-22-6ad7:
/// it is read off the branch like every other set now, and its own Approval is what says so —
/// which is why the rule survived the removal of the record as a source.
/// </para>
/// <para>
/// 2026-09-22-8b25: with ONE exception, which is deliberate rather than an erosion of the rule. A
/// DEMAND is a person saying, in the only channel they have, that this set is to be cut again —
/// the immunity exists so a passing remark cannot replace a ratified cut, not so that nobody can.
/// The re-cut set records the demand as a new approval, so it stays approved and the next
/// ordinary comment is ignored exactly as before.
/// </para>
/// <para>
/// Extracted from <see cref="SpecSourceResolver"/>: choosing the SOURCE and deciding whether the
/// chosen set is amended are two questions, and only the second one has a rule worth this much
/// prose.
/// </para>
/// </summary>
public static class SpecAmendmentRule
{
    /// <summary>True when the deriver has to run over the set the branch carries.</summary>
    public static bool NeedsModel(string cause, SpecSet set)
    {
        ArgumentNullException.ThrowIfNull(set);
        if (string.Equals(cause, SpecRevisionCause.RecutDemand, StringComparison.Ordinal)) return true;
        if (set.Approval is not null) return false;
        return cause switch
        {
            SpecRevisionCause.Comment or SpecRevisionCause.TicketEdit => true,
            SpecRevisionCause.Retrigger => set.Executed.Count == 0,
            _ => false,
        };
    }
}
