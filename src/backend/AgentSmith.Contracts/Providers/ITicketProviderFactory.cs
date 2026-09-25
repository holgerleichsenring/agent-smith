using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Contracts.Providers;

/// <summary>
/// Creates the appropriate ITicketProvider based on configuration.
/// </summary>
public interface ITicketProviderFactory
{
    ITicketProvider Create(TrackerConnection config);

    /// <summary>
    /// 2026-09-25-8e51e: the rewrite capability of the same tracker connection, built here
    /// because this is where a tracker's credentials and endpoints are already resolved — a
    /// second factory would be a second copy of that construction. Every tracker answers with
    /// one, Jira's included: refusing is an answer, and it carries the reason.
    /// </summary>
    ITicketRewriter CreateRewriter(TrackerConnection config);
}
