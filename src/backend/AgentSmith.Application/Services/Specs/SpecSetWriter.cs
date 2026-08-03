using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Specs;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// p0393a: one commit per revision, staged path-scoped so the set lands on its own,
/// then pushed immediately. The push is what lets the draft pull request open at
/// this point — a reviewer reading the cut and the discarded list while the run is
/// still working is the whole mechanism, and it needs the commit on the remote.
/// </summary>
public sealed class SpecSetWriter(
    ISandboxFileReaderFactory readerFactory,
    SandboxGitOperations gitOps,
    ILogger<SpecSetWriter> logger) : ISpecSetWriter
{
    public async Task<SpecSetWriteResult> WriteAsync(
        PipelineContext pipeline, RepoConnection carryingRepo, SpecSet set,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(set);
        var matches = SandboxTargets.SandboxesForRepo(pipeline, carryingRepo);
        if (matches.Count == 0)
            return SpecSetWriteResult.Failed($"no sandbox available for repo '{carryingRepo.Name}'");
        // The branch CheckoutSource actually landed on — the same value CommitAndPR
        // pushes to, so the spec commit and the run's work share one branch.
        var branch = pipeline.TryGet<Domain.Entities.Repository>(ContextKeys.Repository, out var r)
            ? r?.CurrentBranch.Value : null;
        if (string.IsNullOrWhiteSpace(branch))
            return SpecSetWriteResult.Failed("the run has no ticket branch to carry the spec set");
        try
        {
            return await CommitAsync(matches[0].Value, carryingRepo, branch!, set, pipeline, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Spec-set commit failed for {Repo}", carryingRepo.Name);
            return SpecSetWriteResult.Failed(ex.Message.Split('\n', 2)[0].Trim());
        }
    }

    private async Task<SpecSetWriteResult> CommitAsync(
        ISandbox sandbox, RepoConnection repo, string branch, SpecSet set,
        PipelineContext pipeline, CancellationToken ct)
    {
        var key = new SpecSetKey(set.Key);
        await WriteFilesAsync(sandbox, key, set, pipeline, ct);
        await gitOps.ForceStageAsync(sandbox, key.Directory, ct);
        if (!await gitOps.HasStagedChangesAsync(sandbox, ct))
        {
            logger.LogInformation("Spec set {Key} is unchanged — no revision commit", key);
            return SpecSetWriteResult.Ok(await gitOps.GetHeadCommitAsync(sandbox, ct));
        }
        await gitOps.CommitAndPushStagedAsync(sandbox, branch, MessageFor(set), repo.Type, ct);
        var sha = await gitOps.GetHeadCommitAsync(sandbox, ct);
        logger.LogInformation(
            "Spec set {Key} revision {Revision} ({Phases} phase(s)) committed as {Sha} on {Branch}",
            key, set.Current.Number, set.Phases.Count, sha, branch);
        return SpecSetWriteResult.Ok(sha);
    }

    private async Task WriteFilesAsync(
        ISandbox sandbox, SpecSetKey key, SpecSet set, PipelineContext pipeline, CancellationToken ct)
    {
        var files = readerFactory.Create(sandbox);
        await files.WriteAsync($"{key.Directory}/{SpecSetIndex.FileName}", SpecSetIndex.Serialize(set), ct);
        foreach (var phase in set.Phases)
        {
            await files.WriteAsync(key.YamlPath(phase.FileStem), phase.Draft.Yaml.TrimEnd() + "\n", ct);
            await files.WriteAsync(key.MarkdownPath(phase.FileStem), phase.Markdown, ct);
        }
        var segments = pipeline.TryGet<IReadOnlyList<TicketSegment>>(ContextKeys.TicketSegments, out var s)
            ? s! : [];
        await files.WriteAsync(
            key.AccountingPath,
            SpecAccountingBuilder.Render(set.Accounting, segments, set.Key), ct);
    }

    private static string MessageFor(SpecSet set) =>
        $"spec: {set.Key} revision {set.Current.Number} ({set.Current.Cause}), "
        + $"{set.Phases.Count} phase(s)";
}
