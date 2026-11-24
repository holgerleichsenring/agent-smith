using AgentSmith.Contracts.Providers;
using Microsoft.Extensions.Logging;
using AgentSmith.Infrastructure.Models;
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
/// without force, which is the refusal this write wants — and since 2026-11-23-c81ea answers it as a
/// <see cref="BranchWriteResult.Conflict"/>, as it does a ref somebody else created first.
/// </para>
/// </summary>
public sealed class GitHubBranchWrite(
    GitHubRepoAddress address, IGitHubClientFactory clientFactory, string token, ILogger logger)
{
    private readonly GitHubTreeRequest _trees = new(address);
    private readonly AbsentDeletionFilter _deletions = new(logger);

    public async Task<BranchWriteResult> WriteAsync(
        string branch, string defaultBranch, IReadOnlyList<RepoFile> files, string message,
        CancellationToken cancellationToken)
    {
        if (files.Count == 0) return BranchWriteResult.Failed("no files to write");
        var client = clientFactory.Create(address, token);
        try
        {
            var reference = $"heads/{branch}";
            var existing = await TryGetAsync(client, reference);
            var parent = existing?.Object.Sha
                ?? (await client.Git.Reference.Get(address.Owner, address.Repo, $"heads/{defaultBranch}")).Object.Sha;
            var filtered = await _deletions.FilterAsync(
                files, (path, _) => ExistsAtAsync(client, parent, path), cancellationToken);
            if (filtered.Refusal is { } refusal) return refusal;
            var tree = await _trees.CreateAsync(client, parent, filtered.Files);
            var commit = await client.Git.Commit.Create(
                address.Owner, address.Repo, new NewCommit(message, tree.Sha, parent));
            return filtered.WithNotes(await MoveRefAsync(client, existing is not null, branch, commit.Sha, filtered.Files.Count));
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "{Owner}/{Repo}: writing {Branch} failed", address.Owner, address.Repo, branch);
            return BranchWriteResult.Failed(ex.Message.Split('\n', 2)[0].Trim());
        }
    }

    // A 422 on the ref is GitHub saying the head moved (not a fast forward) or the ref appeared meanwhile.
    private async Task<BranchWriteResult> MoveRefAsync(
        IGitHubClient client, bool exists, string branch, string sha, int count)
    {
        try
        {
            if (exists)
                await client.Git.Reference.Update(
                    address.Owner, address.Repo, $"heads/{branch}", new ReferenceUpdate(sha, force: false));
            else
                await client.Git.Reference.Create(address.Owner, address.Repo, new NewReference($"refs/heads/{branch}", sha));
        }
        catch (ApiValidationException ex)
        {
            logger.LogInformation(ex, "{Owner}/{Repo}: {Branch} moved under the write", address.Owner, address.Repo, branch);
            return BranchWriteResult.Conflicted($"`{branch}` moved while the commit was made: {ex.Message.Split('\n', 2)[0].Trim()}");
        }
        logger.LogInformation(
            "{Owner}/{Repo}: {Count} file(s) committed as {Sha} on {Branch}", address.Owner, address.Repo, count, sha, branch);
        return BranchWriteResult.Ok(sha);
    }

    // Only NotFound is "absent"; any other failure throws, so the filter fails the write rather than drop a deletion.
    private async Task<bool> ExistsAtAsync(IGitHubClient client, string sha, string path)
    {
        try
        {
            return (await client.Repository.Content.GetAllContentsByRef(address.Owner, address.Repo, path, sha)).Count > 0;
        }
        catch (NotFoundException ex)
        {
            logger.LogDebug(ex, "{Owner}/{Repo}: {Path} is absent at {Sha}", address.Owner, address.Repo, path, sha);
            return false;
        }
    }

    private async Task<Reference?> TryGetAsync(IGitHubClient client, string reference)
    {
        try { return await client.Git.Reference.Get(address.Owner, address.Repo, reference); }
        catch (NotFoundException) { return null; }
    }
}
