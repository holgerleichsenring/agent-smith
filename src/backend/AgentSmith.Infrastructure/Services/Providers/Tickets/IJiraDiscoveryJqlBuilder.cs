using AgentSmith.Contracts.Models.Triggers;

namespace AgentSmith.Infrastructure.Services.Providers.Tickets;

/// <summary>
/// Translates a <see cref="DiscoveryQuery"/> into the JQL body (OR of per-project status
/// branches) that selects only claimable issues. Jira pushes STATUS only — the tag match
/// stays in the in-process matcher because JQL <c>labels =</c> is case-sensitive.
/// </summary>
public interface IJiraDiscoveryJqlBuilder
{
    string BuildJql(DiscoveryQuery query);
}
