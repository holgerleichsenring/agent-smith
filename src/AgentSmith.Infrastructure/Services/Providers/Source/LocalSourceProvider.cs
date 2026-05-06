using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Domain.Models;
using LibGit2Sharp;
using Repository = AgentSmith.Domain.Entities.Repository;

namespace AgentSmith.Infrastructure.Services.Providers.Source;

/// <summary>
/// Source provider for local git repositories already on disk.
/// </summary>
public sealed class LocalSourceProvider(string basePath) : ISourceProvider
{
    public string ProviderType => "Local";

    public Task<Repository> CheckoutAsync(
        BranchName? branch, CancellationToken cancellationToken)
    {
        ValidateRepositoryPath();

        using var repo = new LibGit2Sharp.Repository(basePath);

        var currentBranch = new BranchName(repo.Head.FriendlyName);

        if (branch is not null && branch.Value != currentBranch.Value)
        {
            var existing = repo.Branches[branch.Value];
            if (existing is not null)
            {
                Commands.Checkout(repo, existing);
            }
            else
            {
                var newBranch = repo.CreateBranch(branch.Value);
                Commands.Checkout(repo, newBranch);
            }
        }

        var remoteUrl = GetRemoteUrl(repo);
        var result = new Repository(basePath, branch ?? currentBranch, remoteUrl);
        return Task.FromResult(result);
    }

    public Task<string> CreatePullRequestAsync(
        Repository repository, string title, string description,
        CancellationToken cancellationToken)
    {
        // Local provider cannot create PRs - branch is pushed, user creates PR manually
        var result = $"Local repository - no PR created, branch pushed: {repository.CurrentBranch}";
        return Task.FromResult(result);
    }

    private void ValidateRepositoryPath()
    {
        if (!Directory.Exists(basePath))
            throw new ProviderException(ProviderType, $"Repository path does not exist: {basePath}");

        if (!LibGit2Sharp.Repository.IsValid(basePath))
            throw new ProviderException(ProviderType, $"Path is not a valid git repository: {basePath}");
    }

    private static string GetRemoteUrl(LibGit2Sharp.Repository repo)
    {
        var remote = repo.Network.Remotes["origin"];
        return remote?.Url ?? "";
    }

}
