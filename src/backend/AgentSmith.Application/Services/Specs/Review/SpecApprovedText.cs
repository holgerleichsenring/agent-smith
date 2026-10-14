using AgentSmith.Contracts.Specs;

namespace AgentSmith.Application.Services.Specs.Review;

/// <summary>
/// 2026-10-13-8762g: whether a spec is still the text a person approved — the same id's spec in the approved
/// record (<see cref="ApprovedSpecSetResolver.Published"/>), its yaml ordinal-equal. The set's approval field
/// is not the test: a re-cut by the run's model keeps the approval on text no design turn reviewed. A
/// serialisation difference costs a review, never a skip.
/// </summary>
public static class SpecApprovedText
{
    public static bool Matches(SpecApprovalRecord? approved, DerivedSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);
        return approved?.Set.Specs.Any(p => p.SpecId == spec.SpecId
            && string.Equals(p.Draft.Yaml, spec.Draft.Yaml, StringComparison.Ordinal)) == true;
    }
}
