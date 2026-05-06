using AgentSmith.Domain.Exceptions;
using LibGit2Sharp;
using LibGit2Sharp.Handlers;
using Microsoft.Extensions.Logging;
using Signature = LibGit2Sharp.Signature;

namespace AgentSmith.Infrastructure.Services.Providers.Source;

/// <summary>
/// LibGit2Sharp operations (clone, checkout, commit, push) for Azure DevOps repos.
/// Used by <see cref="AzureReposSourceProvider"/> to separate git plumbing from API calls.
/// </summary>
internal sealed class AzureGitOperations(string personalAccessToken, ILogger logger)
{
    public void EnsureCloned(string cloneUrl, string localPath)
    {
        if (Repository.IsValid(localPath))
        {
            logger.LogDebug("Repository already cloned at {Path}", localPath);
            return;
        }

        logger.LogInformation("Cloning {Url} to {Path}", cloneUrl, localPath);
        var options = new CloneOptions
        {
            FetchOptions = { CredentialsProvider = GetCredentialsHandler() }
        };
        Repository.Clone(cloneUrl, localPath, options);
    }

    public Branch CheckoutBranch(Repository repo, string branchName)
    {
        // p0112: resume from existing remote work-branch when present.
        // Walk: (1) local exists → checkout. (2) remote tracking origin/<name> exists →
        //   create local tracking branch + checkout (preserves prior WIP commits from
        //   PersistWorkBranchHandler on a previous run for the same ticket).
        // (3) neither → fresh branch from current HEAD (legacy behavior).
        var local = repo.Branches[branchName];
        if (local is not null)
        {
            Commands.Checkout(repo, local);
            logger.LogInformation("Checked out existing local branch {Branch}", branchName);
            return local;
        }

        FetchAllRemotes(repo);
        var remote = repo.Branches[$"origin/{branchName}"];
        if (remote is not null)
        {
            var resumed = repo.CreateBranch(branchName, remote.Tip);
            repo.Branches.Update(resumed, b => b.TrackedBranch = remote.CanonicalName);
            Commands.Checkout(repo, resumed);
            logger.LogInformation(
                "Resumed work branch {Branch} from origin (last commit {Sha} by {Author} at {When})",
                branchName, remote.Tip.Sha[..7], remote.Tip.Author.Name, remote.Tip.Author.When.ToString("o"));
            return resumed;
        }

        var fresh = repo.CreateBranch(branchName);
        Commands.Checkout(repo, fresh);
        logger.LogInformation("Created fresh branch {Branch} from HEAD", branchName);
        return fresh;
    }

    private void FetchAllRemotes(Repository repo)
    {
        try
        {
            var remote = repo.Network.Remotes["origin"]
                ?? throw new ProviderException("AzureRepos", "No 'origin' remote configured.");
            var refSpecs = remote.FetchRefSpecs.Select(r => r.Specification);
            var fetchOptions = new FetchOptions { CredentialsProvider = GetCredentialsHandler() };
            Commands.Fetch(repo, remote.Name, refSpecs, fetchOptions, logMessage: null);
        }
        catch (Exception ex)
        {
            // Non-fatal: if fetch fails, we proceed with fresh-branch fallback.
            logger.LogWarning(ex, "Fetch failed during checkout — proceeding without remote-resume probe");
        }
    }

    private CredentialsHandler GetCredentialsHandler() =>
        (_, _, _) =>
            new UsernamePasswordCredentials { Username = string.Empty, Password = personalAccessToken };
}
