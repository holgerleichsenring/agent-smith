using AgentSmith.Application.Services.Deployment;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Pocs;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Pocs.GitOps;

/// <summary>
/// 2026-11-23-c81eb: commits a PoC's planned deploy folder straight onto the source repo's default branch (c81ea's
/// commit mode, re-planned from the new head when another commit landed first), then — and only then — records the
/// desired state and the seal fingerprint on the row. A folder that already holds the render commits nothing and only
/// records, so a retried run ends the same. A sealed file an unchanged seal keeps but the branch lost clears the
/// fingerprint and fails naming it, so the retry seals again. poc-deploy publishes PocDeployedEvent on every good end.
/// </summary>
public sealed class PocGitOpsCommit(
    ISourceProviderFactory sources, PocGitOpsPlanner planner, GitOpsDelivery delivery, IPocGitOpsStore store, PocDeployedPublisher deployed)
{
    public async Task<CommandResult> RunAsync(PocGitOpsJob job, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(job);
        var provider = sources.Create(job.Repo);
        var branch = await provider.GetDefaultBranchAsync(ct);
        var (plan, skipped, refused) = await planner.PlanAsync(provider, branch, job.Input, job.Folder, job.Bounds.MaxFolderFiles, ct);
        if (plan is null) return CommandResult.Fail($"The deploy folder of '{job.Poc.Name}' was not rendered: {refused}.");
        if (await LostAsync(job, plan) is { } lost) return CommandResult.Fail(lost);
        string? sha = null;
        if (!plan.IsEmpty)
        {
            var (committed, commitSha, sentence) = await delivery.CommitAsync(Order(job, plan), next => ReplanAsync(job, provider, branch, next),
                job.Bounds.CommitAttempts, ct);
            if (!committed) return CommandResult.Fail($"Revision {job.Desired.Revision} of '{job.Poc.Name}' was not committed: {sentence}.");
            sha = commitSha;
        }
        await store.RecordDeployedAsync(job.Poc.PocId, job.Desired, job.Input.Seal.SealFingerprint, ct);
        if (sha is not null) job.Pipeline.Set(ContextKeys.PocCommitSha, sha);
        if (!job.FromAct) await deployed.PublishAsync(job.Pipeline, job.Poc.PocId, job.Desired, sha, job.Actor, ct);
        return CommandResult.Ok(Said(job, plan, sha, skipped));
    }

    private async Task<string?> LostAsync(PocGitOpsJob job, PocFolderChanges plan)
    {
        if (plan.Missing.Count == 0) return null;
        await store.ClearSealFingerprintAsync(job.Poc.PocId, CancellationToken.None);
        return $"{job.Folder}/{plan.Missing[0]} is missing on the branch although the seal is unchanged; the seal fingerprint of "
            + $"'{job.Poc.Name}' is cleared, so a retry seals it again.";
    }

    private async Task<GitOpsCommitOrder?> ReplanAsync(PocGitOpsJob job, ISourceProvider provider, BranchName branch, CancellationToken ct)
    {
        var (plan, _, refused) = await planner.PlanAsync(provider, branch, job.Input, job.Folder, job.Bounds.MaxFolderFiles, ct);
        if (plan is null) throw new InvalidOperationException($"the re-plan at the new head failed: {refused}");
        if (await LostAsync(job, plan) is { } lost) throw new InvalidOperationException(lost);
        return plan.IsEmpty ? null : Order(job, plan);
    }

    private static GitOpsCommitOrder Order(PocGitOpsJob job, PocFolderChanges plan) =>
        new(job.Repo, plan.Changes, $"poc {job.Poc.Name}: revision {job.Desired.Revision}");

    private static string Said(PocGitOpsJob job, PocFolderChanges plan, string? sha, IReadOnlyList<string> skipped)
    {
        var done = sha is null
            ? $"the deploy folder {job.Folder}/ already holds revision {job.Desired.Revision} of '{job.Poc.Name}'; nothing was committed"
            : $"revision {job.Desired.Revision} of '{job.Poc.Name}' is commit {sha} in {job.Repo.Name}:{job.Folder}/ "
              + $"({plan.Changes.Count(c => !c.Delete)} written, {plan.Changes.Count(c => c.Delete)} deleted)";
        var notes = skipped.Count == 0 ? string.Empty : $" The app template's {string.Join(", ", skipped)} is not YAML and was skipped.";
        return $"{char.ToUpperInvariant(done[0])}{done[1..]}, {job.Desired.Replicas} replica(s).{notes} The rollout follow arrives with 2026-11-23-c81ec.";
    }
}
