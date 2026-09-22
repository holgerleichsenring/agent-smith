using AgentSmith.Contracts.Providers;
using Microsoft.Extensions.Logging;
using Octokit;

namespace AgentSmith.Infrastructure.Services.Providers.Source;

/// <summary>
/// 2026-09-22-b6ad: one commit carrying every file, through the SAME authenticated Octokit client
/// the provider already reads a file with — the git-database endpoints, which are the only ones
/// that can put several files in one commit: a tree over the parent's tree, a commit over that
/// tree, then the ref.
/// <para>
/// The ref is CREATED when it is absent and UPDATED with force off when it is present, so an
/// existing branch is committed onto and never rewound. GitHub refuses a non-fast-forward update
/// without force, which is the refusal this write wants.
/// </para>
/// </summary>
public sealed class GitHubBranchWrite(
    string owner, string repo, IGitHubClientFactory clientFactory, string token, ILogger logger)
{
    private const string FileMode = "100644";

    public async Task<BranchWriteResult> WriteAsync(
        string branch, string defaultBranch, IReadOnlyList<RepoFile> files, string message,
        CancellationToken cancellationToken)
    {
        if (files.Count == 0) return BranchWriteResult.Failed("no files to write");
        var client = clientFactory.Create(token);
        try
        {
            var reference = $"heads/{branch}";
            var existing = await TryGetAsync(client, reference);
            var parent = existing?.Object.Sha
                ?? (await client.Git.Reference.Get(owner, repo, $"heads/{defaultBranch}")).Object.Sha;
            var tree = await CreateTreeAsync(client, parent, files);
            var commit = await client.Git.Commit.Create(
                owner, repo, new NewCommit(message, tree.Sha, parent));
            if (existing is null)
                await client.Git.Reference.Create(
                    owner, repo, new NewReference($"refs/heads/{branch}", commit.Sha));
            else
                await client.Git.Reference.Update(
                    owner, repo, reference, new ReferenceUpdate(commit.Sha, force: false));
            logger.LogInformation(
                "{Owner}/{Repo}: {Count} file(s) committed as {Sha} on {Branch}",
                owner, repo, files.Count, commit.Sha, branch);
            return BranchWriteResult.Ok(commit.Sha);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "{Owner}/{Repo}: writing {Branch} failed", owner, repo, branch);
            return BranchWriteResult.Failed(ex.Message.Split('\n', 2)[0].Trim());
        }
    }

    // The parent's tree is the base, so files this commit does not name survive it.
    private Task<TreeResponse> CreateTreeAsync(
        IGitHubClient client, string parentSha, IReadOnlyList<RepoFile> files)
    {
        var tree = new NewTree { BaseTree = parentSha };
        foreach (var file in files)
            tree.Tree.Add(new NewTreeItem
            {
                Path = file.Path,
                Mode = FileMode,
                Type = TreeType.Blob,
                Content = file.Content,
            });
        return client.Git.Tree.Create(owner, repo, tree);
    }

    private async Task<Reference?> TryGetAsync(IGitHubClient client, string reference)
    {
        try { return await client.Git.Reference.Get(owner, repo, reference); }
        catch (NotFoundException) { return null; }
    }
}
