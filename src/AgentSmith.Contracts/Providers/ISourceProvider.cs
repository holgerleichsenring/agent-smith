using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;

namespace AgentSmith.Contracts.Providers;

/// <summary>
/// Provides git operations for a source repository.
/// </summary>
public interface ISourceProvider : ITypedProvider
{
    Task<Repository> CheckoutAsync(BranchName? branch, CancellationToken cancellationToken);

    /// <summary>
    /// Creates a pull request. When <paramref name="linkedTicketId"/> is set,
    /// the provider also associates the ticket with the PR using the platform's
    /// native mechanism — AzDO attaches it as a Work Item Ref, GitHub/GitLab
    /// add a "Closes #N"-style auto-link to the description. Providers without
    /// a sensible link mechanism ignore the parameter.
    /// </summary>
    Task<string> CreatePullRequestAsync(
        Repository repository,
        string title,
        string description,
        CancellationToken cancellationToken,
        TicketId? linkedTicketId = null);
}
