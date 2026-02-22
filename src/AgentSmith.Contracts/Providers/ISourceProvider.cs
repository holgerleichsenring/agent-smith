using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;

namespace AgentSmith.Contracts.Providers;

/// <summary>
/// Provides git operations for a source repository.
/// </summary>
public interface ISourceProvider
{
    string ProviderType { get; }

    Task<Repository> CheckoutAsync(BranchName branch, CancellationToken cancellationToken = default);

    Task<string> CreatePullRequestAsync(
        Repository repository,
        string title,
        string description,
        CancellationToken cancellationToken = default);

    Task CommitAndPushAsync(
        Repository repository,
        string message,
        CancellationToken cancellationToken = default);
}
