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
        BranchName branch, CancellationToken cancellationToken = default)
    {
        ValidateRepositoryPath();

        using var repo = new LibGit2Sharp.Repository(basePath);
        var newBranch = repo.CreateBranch(branch.Value);
        Commands.Checkout(repo, newBranch);

        var remoteUrl = GetRemoteUrl(repo);
        var result = new Repository(basePath, branch, remoteUrl);
        return Task.FromResult(result);
    }

    public Task CommitAndPushAsync(
        Repository repository, string message, CancellationToken cancellationToken = default)
    {
        using var repo = new LibGit2Sharp.Repository(repository.LocalPath);

        StageAllChanges(repo);
        CommitChanges(repo, message);
        PushToRemote(repo);

        return Task.CompletedTask;
    }

    public Task<string> CreatePullRequestAsync(
        Repository repository, string title, string description,
        CancellationToken cancellationToken = default)
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

    private static void StageAllChanges(LibGit2Sharp.Repository repo)
    {
        Commands.Stage(repo, "*");
    }

    private static void CommitChanges(LibGit2Sharp.Repository repo, string message)
    {
        var signature = GetSignature(repo);
        repo.Commit(message, signature, signature);
    }

    private static Signature GetSignature(LibGit2Sharp.Repository repo)
    {
        var config = repo.Config;
        var name = config.GetValueOrDefault("user.name", "Agent Smith");
        var email = config.GetValueOrDefault("user.email", "agent-smith@noreply.local");
        return new Signature(name, email, DateTimeOffset.Now);
    }

    private static void PushToRemote(LibGit2Sharp.Repository repo)
    {
        var remote = repo.Network.Remotes["origin"];
        if (remote is null) return;

        var branch = repo.Head;
        repo.Network.Push(branch);
    }
}
