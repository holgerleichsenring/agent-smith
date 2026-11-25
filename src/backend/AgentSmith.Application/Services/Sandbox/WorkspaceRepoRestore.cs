using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Sandbox.Wire;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// 2026-11-25-9992b: brings one repository back into a replaced workspace exactly where it was saved — a full clone,
/// then its branch at the saved commit, never the checkout's branch ladder, which may cut afresh or merge the base —
/// and redoes what the checkout stage left outside git for each of its keys: the committing identity, the staged feed
/// credentials and the context's prerequisites. A head that does not match fails the restore by name.
/// </summary>
public sealed class WorkspaceRepoRestore(
    IGitTokenResolver credentials, SandboxGitIdentity identity, SandboxTargets targets,
    MidRunRegistryAuth registryAuth, PrerequisiteInstall prerequisites, ILogger<WorkspaceRepoRestore> logger)
{
    /// <summary>Restores <paramref name="state"/>; returns a sentence per prerequisite that did not install.</summary>
    public async Task<IReadOnlyList<string>> RestoreAsync(
        PipelineContext pipeline, WorkspaceRepoState state, CancellationToken cancellationToken)
    {
        var keys = targets.SandboxesForRepo(pipeline, state.Repo);
        if (keys.Count == 0) throw Failed(state, "it has no sandbox in the new workspace");
        await PlaceAsync(state, keys[0].Value, cancellationToken);
        var notes = new List<string>();
        pipeline.TryGet<IReadOnlyDictionary<string, RemoteContextDiscovery>>(ContextKeys.SandboxDiscoveries, out var discoveries);
        foreach (var (key, sandbox) in keys)
        {
            await registryAuth.StageAsync(pipeline, key, sandbox, cancellationToken);
            var outcome = await prerequisites.RunAsync(
                key, sandbox, discoveries?.GetValueOrDefault(key)?.Prerequisites, cancellationToken);
            if (!outcome.Skipped && outcome.ExitCode != 0)
                notes.Add($"{key}: its prerequisites exited {outcome.ExitCode} after the restart");
        }
        logger.LogInformation("{Repo}: restored at {Branch} {Head}", state.Repo.Name, state.Branch, state.Head);
        return notes;
    }

    private async Task PlaceAsync(WorkspaceRepoState state, ISandbox sandbox, CancellationToken ct)
    {
        var root = sandbox.WorkDirectory();
        var credential = credentials.For(state.Repo);
        if (CheckoutStepFactory.BuildMakeRootStep(root) is { } makeRoot)
            await RequireAsync(state, sandbox, makeRoot, "its root could not be made", ct);
        await RequireAsync(state, sandbox, CheckoutStepFactory.BuildCloneStep(state.Repo, credential, root),
            "the clone failed", ct);
        await RequireAsync(state, sandbox, Git(["checkout", "-B", state.Branch, state.Head]),
            $"{state.Head} is not on its remote", ct);
        var head = await sandbox.RunStepAsync(Git(["rev-parse", "HEAD"]), null, ct);
        if (head.OutputContent?.Trim() != state.Head)
            throw Failed(state, $"the clone stands at {head.OutputContent?.Trim()}, not at the saved {state.Head}");
        await identity.EnsureConfiguredAsync(sandbox, state.Repo.Machine, ct); // 9992h: the machine commits
    }

    private static async Task RequireAsync(
        WorkspaceRepoState state, ISandbox sandbox, Step step, string meaning, CancellationToken ct)
    {
        var result = await sandbox.RunStepAsync(step, null, ct);
        if (result.ExitCode != 0)
            throw Failed(state, $"{meaning} (exit {result.ExitCode}: {result.ErrorMessage?.Trim()})");
    }

    private static Step Git(IReadOnlyList<string> args) =>
        new(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Run, Command: "git", Args: args);

    private static WorkspaceRestoreException Failed(WorkspaceRepoState state, string why) =>
        new($"The workspace was restarted, but {state.Repo.Name} could not be restored at {state.Branch} {state.Head}: {why}.");
}
