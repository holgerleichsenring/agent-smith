using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Specs;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-10-12-f5f7f: writes, stages, commits and pushes ONE member's sub-series — what the series
/// writer did per member until a write could fail half-way. The series' manifest and its planned
/// specs are written, every planned file of the series the render left behind is removed with git rm,
/// and only the written paths are force-staged (2026-10-06-03c7d). A member whose sub-series is
/// unchanged gets no commit: its sha is the last commit on the series' paths and nothing is pushed.
/// </summary>
public sealed class SeriesMemberCommit(
    ISandboxFileReaderFactory readerFactory,
    SandboxGitOperations gitOps,
    SeriesFiles files,
    SeriesStaleFiles staleFiles,
    ILogger<SeriesMemberCommit> logger)
{
    /// <param name="plain">Push without force, as every member of a multi-repository write does.</param>
    public async Task<(string Sha, bool Pushed)> CommitAsync(
        ISandbox sandbox, RepoConnection repo, string branch, SpecSet set, bool plain, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(set);
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
            logger.LogInformation("Series {Base} is unchanged in {Repo} — no revision commit", set.Series, repo.Name);
            return (await gitOps.GetLastCommitForPathsAsync(sandbox, SeriesReader.RevisionPaths(set.Series!), ct), false);
        }
        await gitOps.CommitAndPushStagedAsync(sandbox, branch, SeriesWriter.MessageFor(set), repo, ct, plain);
        var sha = await gitOps.GetHeadCommitAsync(sandbox, ct);
        logger.LogInformation(
            "Series {Base} revision {Revision} ({Phases} spec(s)) committed in {Repo} as {Sha} on {Branch}",
            set.Series, set.Current.Number, set.Phases.Count, repo.Name, sha, branch);
        return (sha, true);
    }

    /// <summary>The paths a write of <paramref name="set"/> puts in a checkout.</summary>
    public IReadOnlyList<string> PathsOf(SpecSet set) => [.. files.Render(set).Select(f => f.Path)];

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
}
