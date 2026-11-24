using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Pocs;
using AgentSmith.Application.Services.Pocs.GitOps;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Exceptions;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Pocs;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// 2026-11-23-c81eb: the GitOps commit of a PoC run — gathers the desired state (an act's, or poc-deploy's built
/// revision with one replica), the seal (a render without one is refused: SealPocVariables runs first in every chain that
/// renders) and the render input, and hands them to <see cref="PocGitOpsCommit"/>, which renders
/// &lt;deploy_root&gt;/&lt;name&gt;/ from the app template and commits it. The run holds the PoC's run gate, so no other
/// run of it writes meanwhile.
/// <para>2026-11-23-c81ed: a delete (ContextKeys.PocRolloutAbsent) renders nothing and needs no seal: it goes to
/// <see cref="PocGitOpsDelete"/>, which removes the folder.</para>
/// </summary>
public sealed class GitOpsCommitHandler(
    IPocGitOpsStore store, PocsConfigSource config, PocDesiredStates desired, PocRenderInputs inputs, PocGitOpsCommit commit,
    PocGitOpsDelete delete, ILogger<GitOpsCommitHandler> logger) : ICommandHandler<GitOpsCommitContext>
{
    public async Task<CommandResult> ExecuteAsync(GitOpsCommitContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.Seeds.PocId is not { } pocId || await store.ReadAsync(pocId, cancellationToken) is not { } poc)
            return CommandResult.Fail("A PoC run's GitOps commit needs the PoC it was seeded with; none was found.");
        var pocs = config.Current();
        if (pocs.Targets.FirstOrDefault(t => string.Equals(t.Name, poc.Target, StringComparison.OrdinalIgnoreCase)) is not { } target)
            return CommandResult.Fail($"'{poc.Name}' names the target '{poc.Target}', which pocs.targets no longer holds.");
        if (context.Project.Repos.FirstOrDefault() is not { } repo)
            return CommandResult.Fail($"The project '{context.Project.Name}' names no source repo to commit '{poc.Name}' to.");
        if (context.Pipeline.TryGet<bool>(ContextKeys.PocRolloutAbsent, out var absent) && absent)
            return await DeleteAsync(context, poc, repo, $"{target.DeployRoot.TrimEnd('/')}/{poc.Name}", pocs.Gitops, cancellationToken);
        if (poc.IsGone) return CommandResult.Fail($"'{poc.Name}' is {(poc.State == PocStates.Deleted ? "deleted" : "being deleted")}; nothing is rendered into its deploy folder.");
        if (!context.Pipeline.TryGet<PocSealResult>(ContextKeys.PocSeal, out var seal) || seal is null)
            return CommandResult.Fail($"The manifests of '{poc.Name}' are not rendered: the seal step did not run (SealPocVariables comes first).");
        var (state, fromAct, unresolved) = await desired.ResolveAsync(context.Pipeline, poc, context.Seeds, cancellationToken);
        if (state is null) return CommandResult.Fail(unresolved!);
        return await CommitAsync(context, poc, (state, fromAct), target, seal, repo, pocs.Gitops, cancellationToken);
    }

    private async Task<CommandResult> DeleteAsync(
        GitOpsCommitContext context, PocGitOpsSubject poc, RepoConnection repo, string folder, PocGitOpsConfig bounds, CancellationToken ct)
    {
        try
        {
            return await delete.RunAsync(new PocGitOpsDeleteJob(context.Pipeline, poc, repo, folder, bounds, context.Seeds.Actor), ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException)
        {
            logger.LogWarning(ex, "The delete commit of PoC {PocId} failed", poc.PocId);
            return CommandResult.Fail($"The deploy folder of '{poc.Name}' was not removed: {ex.Message}");
        }
    }

    private async Task<CommandResult> CommitAsync(
        GitOpsCommitContext context, PocGitOpsSubject poc, (PocDesiredState State, bool FromAct) desiredState, PocTargetConfig target,
        PocSealResult seal, RepoConnection repo, PocGitOpsConfig bounds, CancellationToken ct)
    {
        try
        {
            var (input, refused) = await inputs.BuildAsync(poc, desiredState.State, target, seal, ct);
            if (input is null) return CommandResult.Fail(refused!);
            var folder = $"{target.DeployRoot.TrimEnd('/')}/{poc.Name}";
            return await commit.RunAsync(new PocGitOpsJob(context.Pipeline, poc, desiredState.State, desiredState.FromAct, input, repo,
                folder, bounds, context.Seeds.Actor), ct);
        }
        catch (Exception ex) when (ex is ContainerRegistryException or HttpRequestException or InvalidOperationException)
        {
            logger.LogWarning(ex, "The GitOps commit of PoC {PocId} failed", poc.PocId);
            return CommandResult.Fail($"Revision {desiredState.State.Revision} of '{poc.Name}' was not committed: {ex.Message}");
        }
    }
}
