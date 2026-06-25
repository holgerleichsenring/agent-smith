namespace AgentSmith.Contracts.Models.Triggers;

/// <summary>
/// The composed ticket-discovery query for one tracker: one <see cref="DiscoveryBranch"/>
/// per routed project (OR'd by the provider builder) plus the native parking statuses a
/// broad branch must exclude. Built by the poller from config; a provider's builder
/// translates it to JQL/WIQL so discovery fetches only claimable candidates instead of
/// every open ticket.
/// </summary>
public sealed record DiscoveryQuery(
    IReadOnlyList<DiscoveryBranch> Branches,
    IReadOnlyList<string> ParkingStatuses);
