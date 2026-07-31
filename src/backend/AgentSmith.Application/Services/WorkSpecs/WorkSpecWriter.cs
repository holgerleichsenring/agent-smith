using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.WorkSpecs;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.WorkSpecs;

/// <summary>
/// p0390: one commit per revision, staged path-scoped so the spec lands on its
/// own, then pushed immediately. The push is what lets the draft PR open at this
/// point — a reviewer editing the spec in the PR while the run is still working
/// is the whole mechanism, and it needs the commit to exist on the remote.
/// </summary>
public sealed class WorkSpecWriter(
    ISandboxFileReaderFactory readerFactory,
    SandboxGitOperations gitOps,
    IWorkSpecSerializer serializer,
    ILogger<WorkSpecWriter> logger) : IWorkSpecWriter
{
    public async Task<WorkSpecWriteResult> WriteAsync(
        PipelineContext pipeline, RepoConnection carryingRepo, WorkSpecArtifact artifact,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        var matches = SandboxTargets.SandboxesForRepo(pipeline, carryingRepo);
        if (matches.Count == 0)
            return WorkSpecWriteResult.Failed($"no sandbox available for repo '{carryingRepo.Name}'");
        // The branch CheckoutSource actually landed on — the same value CommitAndPR
        // pushes to, so the spec commit and the run's work share one branch.
        var branch = pipeline.TryGet<Domain.Entities.Repository>(ContextKeys.Repository, out var r)
            ? r?.CurrentBranch.Value : null;
        if (string.IsNullOrWhiteSpace(branch))
            return WorkSpecWriteResult.Failed("the run has no ticket branch to carry the work spec");
        try
        {
            return await CommitAsync(matches[0].Value, carryingRepo, branch!, artifact, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Work-spec commit failed for {Repo}", carryingRepo.Name);
            return WorkSpecWriteResult.Failed(ex.Message.Split('\n', 2)[0].Trim());
        }
    }

    private async Task<WorkSpecWriteResult> CommitAsync(
        ISandbox sandbox, RepoConnection repo, string branch,
        WorkSpecArtifact artifact, CancellationToken ct)
    {
        var key = new WorkSpecKey(artifact.Spec.Key);
        await WriteFilesAsync(sandbox, key, artifact, ct);
        await gitOps.StagePathAsync(sandbox, key.Directory, ct);
        await gitOps.ForceStageAsync(sandbox, WorkSpecSchemaResource.RepoPath, ct);
        if (!await gitOps.HasStagedChangesAsync(sandbox, ct))
        {
            logger.LogInformation("Work spec {Key} is unchanged — no revision commit", key);
            return WorkSpecWriteResult.Ok(await gitOps.GetHeadCommitAsync(sandbox, ct));
        }
        await gitOps.CommitAndPushStagedAsync(sandbox, branch, MessageFor(artifact.Spec), repo.Type, ct);
        var sha = await gitOps.GetHeadCommitAsync(sandbox, ct);
        logger.LogInformation(
            "Work spec {Key} revision {Revision} committed as {Sha} on {Branch}",
            key, artifact.Spec.Current.Number, sha, branch);
        return WorkSpecWriteResult.Ok(sha);
    }

    private async Task WriteFilesAsync(
        ISandbox sandbox, WorkSpecKey key, WorkSpecArtifact artifact, CancellationToken ct)
    {
        var files = readerFactory.Create(sandbox);
        await files.WriteAsync(key.SpecPath, serializer.Serialize(artifact.Spec), ct);
        await files.WriteAsync(key.SamplesPath, artifact.SamplesMarkdown, ct);
        // Ships the schema the spec.yaml header points at, so a reviewer editing the
        // spec in a repo that has never seen this system still gets validation.
        await files.WriteAsync(WorkSpecSchemaResource.RepoPath, WorkSpecSchemaResource.Text, ct);
    }

    private static string MessageFor(WorkSpec spec) =>
        $"spec: {spec.Key} revision {spec.Current.Number} ({spec.Current.Cause})";
}
