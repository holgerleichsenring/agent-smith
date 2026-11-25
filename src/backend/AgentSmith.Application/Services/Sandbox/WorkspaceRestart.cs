using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// 2026-11-25-9992b: gives the run's workspace a toolchain it lacks by replacing it — Kubernetes cannot add a container
/// to a running pod, and ephemeral containers take no resources and need RBAC the server does not hold. In order, alone
/// at the step gate (steps in flight finish first, new ones wait): every repository's work goes to its remote branch,
/// liveness stops watching, the workspace is replaced, every repository is restored at its saved commit, carried
/// references come back and liveness watches again. A repository that cannot be saved refuses the restart and leaves
/// the workspace as it was. Anything that fails once the old workspace is gone ends the run the way a vanished sandbox
/// does: the tool loop would otherwise go on over a tree it no longer has.
/// </summary>
public sealed class WorkspaceRestart(
    WorkspaceCheckpoint checkpoint, WorkspaceRepoRestore restore, ReferenceMaterializer references,
    IRunCancellationRegistry cancellation, ILogger<WorkspaceRestart> logger)
{
    internal const string EndReason = "sandbox-vanished";

    /// <summary>Returns a sentence per thing that did not come back cleanly; throws
    /// <see cref="WorkspaceRestartRefusedException"/> when a repository cannot be saved.</summary>
    public Task<IReadOnlyList<string>> RestartAsync(
        PipelineContext pipeline, RunWorkspace workspace, WorkspaceSpec spec,
        ISandboxLivenessSupervisor liveness, Action watchAgain, CancellationToken cancellationToken) =>
        workspace.Gate.RunAloneAsync(async () =>
        {
            var save = await checkpoint.SaveAsync(pipeline, cancellationToken);
            if (save.Refusal is { } refusal)
            {
                logger.LogWarning("Workspace restart refused: {Reason}", refusal);
                throw new WorkspaceRestartRefusedException(refusal);
            }
            await liveness.UnwatchAllAsync();
            try
            {
                var notes = await ReplaceAndRestoreAsync(pipeline, workspace, spec, save, cancellationToken);
                // The watchers outlive the restart, so they must not inherit its pass through the step gate.
                using (ExecutionContext.SuppressFlow()) watchAgain();
                return notes;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                throw EndRun(pipeline, ex);
            }
        }, cancellationToken);

    private async Task<IReadOnlyList<string>> ReplaceAndRestoreAsync(
        PipelineContext pipeline, RunWorkspace workspace, WorkspaceSpec spec, WorkspaceSave save, CancellationToken ct)
    {
        await workspace.ReplaceAsync(spec, ct);
        var notes = new List<string>();
        foreach (var state in save.Repos)
            notes.AddRange(await restore.RestoreAsync(pipeline, state, ct));
        if (await references.CarryAsync(pipeline, ct) is { IsSuccess: false } carried)
            notes.Add($"the carried references did not come back: {carried.Message}");
        logger.LogInformation("Workspace restarted with [{Images}]; {Count} repositor(ies) restored",
            string.Join(", ", spec.Toolchains.Select(t => t.Image)), save.Repos.Count);
        return notes;
    }

    private WorkspaceRestoreException EndRun(PipelineContext pipeline, Exception cause)
    {
        var detail = cause is WorkspaceRestoreException ? cause.Message
            : $"The workspace was being restarted with one more toolchain and did not come back: {cause.Message}";
        logger.LogError(cause, "Workspace restart failed after the old workspace was removed; the run ends");
        if (pipeline.TryGet<string>(ContextKeys.RunId, out var runId) && !string.IsNullOrEmpty(runId))
            cancellation.TryCancel(runId, EndReason, detail);
        return cause as WorkspaceRestoreException ?? new WorkspaceRestoreException(detail);
    }
}
