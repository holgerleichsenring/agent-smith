namespace AgentSmith.Contracts.Models.Triggers;

/// <summary>
/// One project's claimable-ticket gate for the composed discovery query: tickets whose
/// native status is in <see cref="Statuses"/> AND that match <see cref="Criterion"/>.
/// Empty <see cref="Statuses"/> = status-unconstrained (accept any status); a null
/// <see cref="Criterion"/> = no resolution match (defensive — every trigger requires one).
/// </summary>
public sealed record DiscoveryBranch(
    IReadOnlyList<string> Statuses,
    DiscoveryCriterion? Criterion);
