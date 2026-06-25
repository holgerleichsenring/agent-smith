using AgentSmith.Contracts.Models.Triggers;

namespace AgentSmith.Infrastructure.Services.Providers.Tickets;

/// <summary>
/// Translates a <see cref="DiscoveryQuery"/> into the WIQL WHERE clause (the OR of
/// per-project branches) that selects only claimable work items, so AzDO returns
/// candidates instead of every open item.
/// </summary>
public interface IAzureDevOpsDiscoveryWiqlBuilder
{
    string BuildWhere(DiscoveryQuery query, IReadOnlyList<string> openStates);
}
