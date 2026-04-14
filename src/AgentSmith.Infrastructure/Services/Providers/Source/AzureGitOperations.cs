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
        var existingBranch = repo.Branches[branchName];
        var targetBranch = existingBranch ?? repo.CreateBranch(branchName);
        Commands.Checkout(repo, targetBranch);
        return targetBranch;
    }

    public void StageAllChanges(Repository repo) =>
        Commands.Stage(repo, "*");

    public void CommitChanges(Repository repo, string message)
    {
        var signature = GetSignature(repo);
        repo.Commit(message, signature, signature);
    }

    public void PushToRemote(Repository repo)
    {
        var remote = repo.Network.Remotes["origin"]
            ?? throw new ProviderException("AzureRepos", "No 'origin' remote configured.");

        var options = new PushOptions
        {
            CredentialsProvider = GetCredentialsHandler()
        };

        var canonicalName = repo.Head.CanonicalName;
        // Force push (+) so re-runs on the same ticket don't fail with
        // "non-fastforwardable reference" when the branch already exists on the remote.
        var refspec = $"+{canonicalName}:{canonicalName}";
        repo.Network.Push(remote, refspec, options);
    }

    private CredentialsHandler GetCredentialsHandler() =>
        (_, _, _) =>
            new UsernamePasswordCredentials { Username = string.Empty, Password = personalAccessToken };

    private static Signature GetSignature(Repository repo)
    {
        var config = repo.Config;
        var name = config.GetValueOrDefault("user.name", "Agent Smith");
        var email = config.GetValueOrDefault("user.email", "agent-smith@noreply.local");
        return new Signature(name, email, DateTimeOffset.Now);
    }
}
