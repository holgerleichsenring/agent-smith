using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Sandbox.Wire;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// 2026-11-25-9992b: puts every repository's work on its remote branch before the workspace is replaced. A dirty tree
/// goes through the checkpoint's own path (staging, the secret scan, commit, push); a clean tree whose commits are
/// on no remote ref is pushed as it is, because a checkpoint pushes only what it commits. A tree that stays dirty, or
/// a push that fails, refuses the save by name — the workspace then stays as it is.
/// </summary>
public sealed class WorkspaceCheckpoint(
    RepoWorkPusher pusher, SandboxGitOperations gitOps, GitBranchPusher branchPusher,
    IGitTokenResolver credentials, SandboxTargets targets, ILogger<WorkspaceCheckpoint> logger)
{
    public async Task<WorkspaceSave> SaveAsync(PipelineContext pipeline, CancellationToken cancellationToken)
    {
        var repos = pipeline.TryGet<IReadOnlyList<RepoConnection>>(ContextKeys.Repos, out var held) && held is not null
            ? held : [];
        var runId = pipeline.TryGet<string>(ContextKeys.RunId, out var id) && id is not null ? id : "unknown";
        var states = new List<WorkspaceRepoState>();
        foreach (var repo in repos)
        {
            if (targets.SandboxesForRepo(pipeline, repo) is not [var (_, sandbox), ..]) continue;
            try
            {
                states.Add(await SaveOneAsync(pipeline, repo, sandbox, runId, cancellationToken));
            }
            catch (InvalidOperationException ex)
            {
                logger.LogWarning(ex, "{Repo}: its work could not be saved before the workspace restart", repo.Name);
                return WorkspaceSave.Refused($"{repo.Name}: {ex.Message}");
            }
        }
        return WorkspaceSave.Saved(states);
    }

    private async Task<WorkspaceRepoState> SaveOneAsync(
        PipelineContext pipeline, RepoConnection repo, ISandbox sandbox, string runId, CancellationToken ct)
    {
        var branch = await GitAsync(sandbox, ["rev-parse", "--abbrev-ref", "HEAD"], ct);
        if (branch is null or "HEAD")
            throw new InvalidOperationException("the tree is on no branch, so its work has no branch to be put on");
        await pusher.PushAsync(pipeline, repo, branch, runId, ct);
        if (await gitOps.HasWorkingChangesAsync(sandbox, ct))
            throw new InvalidOperationException(
                "its changes could not be committed and pushed (the secret scan refused the diff), so a restart would lose them");
        var head = await GitAsync(sandbox, ["rev-parse", "HEAD"], ct)
                   ?? throw new InvalidOperationException("its head could not be read");
        if (string.IsNullOrEmpty(await GitAsync(sandbox, ["branch", "-r", "--contains", "HEAD"], ct)))
            await branchPusher.PushAsync(sandbox, branch, GitStepCredentials.Helper, credentials.For(repo), ct);
        logger.LogInformation("{Repo}: saved at {Branch} {Head} before the workspace restart", repo.Name, branch, head);
        return new WorkspaceRepoState(repo, branch, head);
    }

    private static async Task<string?> GitAsync(ISandbox sandbox, IReadOnlyList<string> args, CancellationToken ct)
    {
        var result = await sandbox.RunStepAsync(new Step(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Run,
            Command: "git", Args: args), null, ct);
        return result.ExitCode == 0 ? result.OutputContent?.Trim() : null;
    }
}
