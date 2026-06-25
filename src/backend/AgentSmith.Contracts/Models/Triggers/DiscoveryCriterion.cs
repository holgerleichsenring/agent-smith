using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Contracts.Models.Triggers;

/// <summary>
/// The resolution match one project applies to claim a ticket — the query-pushdown
/// counterpart of <see cref="ProjectResolutionConfig"/>. A provider's discovery-query
/// builder translates an EXPRESSIBLE criterion (Tag / AreaPath) into JQL/WIQL; a
/// non-expressible one (Repo / ToAddress, or AreaPath on Jira) makes its branch broad.
/// </summary>
public sealed record DiscoveryCriterion(ResolutionStrategy Strategy, string Value);
