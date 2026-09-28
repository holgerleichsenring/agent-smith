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

    /// <summary>
    /// 2026-09-27-5c1ea: the text-search capability of the same tracker connection. Built here for
    /// the reason the rewriter is — this is where a tracker's credentials and endpoints are already
    /// resolved. Every tracker answers with one; each reports a failed query as a refusal, because
    /// a picker that answers "no such ticket" for a query that never ran is worse than one that
    /// says it could not look.
    /// </summary>
    ITicketSearch CreateSearch(TrackerConnection config);

    /// <summary>
    /// 2026-09-28-1da5c: what the tracker links to a ticket — the pull requests, and the branches
    /// where it links those too. Built here for the reason its siblings are: this is where a
    /// tracker's credentials are already resolved, and a model that had to reach them itself would
    /// need a secret rather than a question.
    /// </summary>
    ITicketLinkedWork CreateLinkedWork(TrackerConnection config);
}
