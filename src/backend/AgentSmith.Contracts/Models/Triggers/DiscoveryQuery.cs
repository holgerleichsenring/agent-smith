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
    IReadOnlyList<string> ParkingStatuses)
{
    /// <summary>
    /// Union of the tracker's configured pipeline_from_label trigger keys (e.g.
    /// <c>agent-smith:bug</c>). Providers that can express it push a server-side guard so
    /// discovery fetches only trigger-labelled tickets instead of every project-tagged one;
    /// empty = no guard (broad). Populated by <c>TrackerDiscoveryQueryBuilder</c>.
    /// </summary>
    public IReadOnlyList<string> TriggerLabels { get; init; } = [];

    /// <summary>
    /// 2026-09-25-c1f7: the tracker's own ids of tickets an approved record still expects work
    /// on. They are OR'd with the WHOLE rest of the query, never added to
    /// <see cref="TriggerLabels"/>: the label guard is AND-ed onto the routing clause, so a list
    /// that must ADMIT what the guard excludes cannot live inside it. A ticket whose stamp
    /// somebody deleted is fetched because of this and nothing else.
    /// <para>
    /// Only providers that filter SERVER-SIDE read it — Jira and Azure DevOps. GitHub and GitLab
    /// narrow by the branch tag or list open issues and never read the label guard either, so
    /// there is nothing there for this to widen.
    /// </para>
    /// </summary>
    public IReadOnlyList<string> ApprovedTicketIds { get; init; } = [];
}
