using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Application.Services.Pocs.Commit;

/// <summary>
/// 2026-11-23-933dc: the folder's one commit onto the shared branch with a PLAIN push — never forced, never with a lease
/// that a refetch would turn into an overwrite. A rejected push fetches the branch, rebases the one commit onto the new
/// head (another folder's commit is kept) and pushes plain once more; a second rejection or a rebase conflict fails with
/// git's message, a protected branch's reason included.
/// </summary>
public sealed class PocFolderPush(SandboxGitOperations operations, GitBranchPusher pusher, PocGit git, IGitTokenResolver credentials)
{
    public async Task<string> CommitAndPushAsync(
        ISandbox sandbox, RepoConnection repo, string branch, string message, CancellationToken ct)
    {
        try
        {
            await operations.CommitAndPushStagedAsync(sandbox, branch, message, repo, ct, plain: true);
        }
        catch (InvalidOperationException rejected) when (rejected.Message.StartsWith("git push failed", StringComparison.Ordinal))
        {
            await git.RunAsync(sandbox, $"Fetching '{branch}' after the push was refused ({rejected.Message})",
                ["fetch", "origin", branch], ct, repo);
            await git.RunAsync(sandbox, "Rebasing the PoC commit onto the new head", ["rebase", "FETCH_HEAD"], ct, repo);
            await pusher.PushPlainAsync(sandbox, branch, GitStepCredentials.Helper, credentials.For(repo), ct);
        }
        return await operations.GetHeadCommitAsync(sandbox, ct);
    }
}
