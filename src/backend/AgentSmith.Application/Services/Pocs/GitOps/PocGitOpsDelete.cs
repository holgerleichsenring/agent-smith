using AgentSmith.Application.Services.Deployment;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Pocs;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Pocs.GitOps;

/// <summary>
/// 2026-11-23-c81ed: the GitOps commit of a delete — renders nothing and removes every file of the deploy folder in one
/// commit (re-planned from the new head when another commit landed first); Argo CD's ApplicationSet then drops the
/// Application and prunes it. Once the commit landed, or the folder was already gone, the row's Revision, ImageDigest and
/// seal fingerprint are nulled, so a pending or failed follow can never re-render the folder. The source folder stays.
/// </summary>
public sealed class PocGitOpsDelete(ISourceProviderFactory sources, PocAbsentPlan planner, GitOpsDelivery delivery, IPocGitOpsStore store, PocActedPublisher acted)
{
    public async Task<CommandResult> RunAsync(PocGitOpsDeleteJob job, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(job);
        var provider = sources.Create(job.Repo);
        var branch = await provider.GetDefaultBranchAsync(ct);
        var (plan, refused) = await planner.PlanAsync(provider, branch, job.Folder, job.Bounds.MaxFolderFiles, ct);
        if (plan is null) return CommandResult.Fail($"The deploy folder of '{job.Poc.Name}' was not removed: {refused}.");
        string? sha = null;
        if (!plan.IsEmpty)
        {
            var (committed, commitSha, sentence) = await delivery.CommitAsync(Order(job, plan), next => ReplanAsync(job, provider, branch, next),
                job.Bounds.CommitAttempts, ct);
            if (!committed) return CommandResult.Fail($"The deploy folder of '{job.Poc.Name}' was not removed: {sentence}.");
            sha = commitSha;
        }
        await store.RecordAbsentAsync(job.Poc.PocId, ct);
        if (sha is not null) job.Pipeline.Set(ContextKeys.PocCommitSha, sha);
        await acted.PublishAsync(job.Pipeline, job.Poc.PocId, PocActKinds.Delete, null, sha, job.Actor, ct);
        return CommandResult.Ok(sha is null
            ? $"The deploy folder {job.Folder}/ of '{job.Poc.Name}' is already gone; nothing was committed. Its source stays."
            : $"The deploy folder {job.Folder}/ of '{job.Poc.Name}' is removed in commit {sha} ({plan.Changes.Count} file(s) deleted); "
              + "Argo CD prunes the PoC once it drops the Application. Its source stays.");
    }

    private async Task<GitOpsCommitOrder?> ReplanAsync(PocGitOpsDeleteJob job, ISourceProvider provider, BranchName branch, CancellationToken ct)
    {
        var (plan, refused) = await planner.PlanAsync(provider, branch, job.Folder, job.Bounds.MaxFolderFiles, ct);
        if (plan is null) throw new InvalidOperationException($"the re-plan at the new head failed: {refused}");
        return plan.IsEmpty ? null : Order(job, plan);
    }

    private static GitOpsCommitOrder Order(PocGitOpsDeleteJob job, PocFolderChanges plan) =>
        new(job.Repo, plan.Changes, $"poc {job.Poc.Name}: delete");
}
