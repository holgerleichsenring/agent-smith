using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Specs;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// p0393a: one commit per revision, staged path-scoped so the set lands on its own, then pushed
/// immediately — a reviewer reading the cut while the run is still working needs it on the remote.
/// <para>
/// 2026-10-06-03c7d: the series' manifest and its planned specs are written, every planned file
/// of the series the render left behind is removed with git rm, and only the written paths are
/// force-staged — a path that does not exist would fail the whole add.
/// </para>
/// </summary>
public sealed class SeriesWriter(
    ISandboxFileReaderFactory readerFactory,
    SandboxGitOperations gitOps,
    SeriesFiles files,
    SeriesStaleFiles staleFiles,
    SandboxTargets sandboxTargets,
    ILogger<SeriesWriter> logger) : ISpecSetWriter
{
    public async Task<SpecSetWriteResult> WriteAsync(
        PipelineContext pipeline, RepoConnection carryingRepo, SpecSet set,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(set);
        var matches = sandboxTargets.SandboxesForRepo(pipeline, carryingRepo);
        if (matches.Count == 0)
            return SpecSetWriteResult.Failed($"no sandbox available for repo '{carryingRepo.Name}'");
        // The branch CheckoutSource actually landed on — the same value CommitAndPR pushes to.
        var branch = pipeline.TryGet<Domain.Entities.Repository>(ContextKeys.Repository, out var r)
            ? r?.CurrentBranch.Value : null;
        if (string.IsNullOrWhiteSpace(branch))
            return SpecSetWriteResult.Failed("the run has no ticket branch to carry the spec set");
        try
        {
            return await CommitAsync(matches[0].Value, carryingRepo, branch!, set, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Spec-set commit failed for {Repo}", carryingRepo.Name);
            return SpecSetWriteResult.Failed(ex.Message.Split('\n', 2)[0].Trim());
        }
    }

    private async Task<SpecSetWriteResult> CommitAsync(
        ISandbox sandbox, RepoConnection repo, string branch, SpecSet set, CancellationToken ct)
    {
        var rendered = files.Render(set);
        var reader = readerFactory.Create(sandbox);
        foreach (var file in rendered)
            await reader.WriteAsync(file.Path, file.Content, ct);
        await RemoveStaleAsync(sandbox, reader, set, rendered, ct);
        await gitOps.ForceStagePathsAsync(sandbox, [.. rendered.Select(f => f.Path)], ct);
        if (!await gitOps.HasStagedChangesAsync(sandbox, ct))
        {
            // The sha the next run's reader compares the pointer against: the last commit on the
            // series' paths, never a coding commit at HEAD.
            logger.LogInformation("Series {Base} is unchanged — no revision commit", set.Series);
            return SpecSetWriteResult.Ok(await gitOps.GetLastCommitForPathsAsync(
                sandbox, SeriesReader.RevisionPaths(set.Series!), ct));
        }
        await gitOps.CommitAndPushStagedAsync(sandbox, branch, MessageFor(set), repo, ct);
        var sha = await gitOps.GetHeadCommitAsync(sandbox, ct);
        logger.LogInformation(
            "Series {Base} revision {Revision} ({Phases} spec(s)) committed as {Sha} on {Branch}",
            set.Series, set.Current.Number, set.Phases.Count, sha, branch);
        return SpecSetWriteResult.Ok(sha);
    }

    private async Task RemoveStaleAsync(
        ISandbox sandbox, ISandboxFileReader reader, SpecSet set, IReadOnlyList<SpecSetFile> rendered,
        CancellationToken ct)
    {
        var listed = await reader.ListAsync(SeriesPaths.Planned, maxDepth: 1, ct);
        var stale = staleFiles.Select(listed, set, rendered);
        if (stale.Count == 0) return;
        logger.LogInformation(
            "Series {Base}: removing {Count} file(s) absent from the current cut: {Files}",
            set.Series, stale.Count, string.Join(", ", stale));
        await gitOps.RemoveAsync(sandbox, stale, ct);
    }

    /// <summary>The commit message of a series revision.</summary>
    public static string MessageFor(SpecSet set) =>
        $"spec: {set.Series} ({set.Key}) revision {set.Current.Number} ({set.Current.Cause}), "
        + $"{set.Phases.Count} spec(s)";
}
