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

    public Task CommitAndPushAsync(
        Repository repository, string message, CancellationToken cancellationToken)
    {
        using var repo = new LibGit2Sharp.Repository(repository.LocalPath);

        StageAllChanges(repo);
        CommitChanges(repo, message);
        PushToRemote(repo);

        return Task.CompletedTask;
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

    internal static void StageAllChanges(LibGit2Sharp.Repository repo)
    {
        // Per-file staging via RetrieveStatus instead of Commands.Stage(repo, "*").
        // The "*" glob expands inside libgit2 and can return directory-shaped paths
        // (e.g. "RHS.CICD/") that git_index_add_bypath then rejects with
        // "invalid path: ...". Iterating workdir entries and staging each file path
        // sidesteps the glob entirely.
        var status = repo.RetrieveStatus(new StatusOptions
        {
            IncludeIgnored = false,
            IncludeUntracked = true,
            RecurseUntrackedDirs = true
        });
        foreach (var entry in status)
        {
            if (IsWorkdirChange(entry.State))
            {
                Commands.Stage(repo, entry.FilePath);
            }
        }
    }

    private static bool IsWorkdirChange(FileStatus state) =>
        state.HasFlag(FileStatus.NewInWorkdir) ||
        state.HasFlag(FileStatus.ModifiedInWorkdir) ||
        state.HasFlag(FileStatus.DeletedFromWorkdir) ||
        state.HasFlag(FileStatus.RenamedInWorkdir) ||
        state.HasFlag(FileStatus.TypeChangeInWorkdir);

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
