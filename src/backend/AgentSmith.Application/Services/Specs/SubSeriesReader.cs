using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Specs;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-10-12-f5f7b: reads ONE repository's sub-series of a ticket — what the series reader did for
/// the carrying repository alone until a ticket's specs could lie in several. One sandbox per
/// repository; the manifest found by ticket key lists its spec ids, each read across planned/,
/// done/ and active/ of the same checkout (2026-10-06-03c7d/e).
/// 2026-11-17-a0b6a: the reading is <see cref="SubSeriesFiles"/>'s, shared with the host-side branch
/// read; this class picks the checkout and asks git for the revision sha.
/// </summary>
public sealed class SubSeriesReader(
    ISandboxFileReaderFactory readerFactory,
    SandboxGitOperations gitOps,
    SubSeriesFiles subSeries,
    SandboxTargets sandboxTargets)
{
    public async Task<SubSeriesRead> ReadAsync(
        PipelineContext pipeline, RepoConnection repo, TicketKey ticket, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(repo);
        var name = repo.Name ?? string.Empty;
        var matches = sandboxTargets.SandboxesForRepo(pipeline, repo);
        if (matches.Count == 0) return new SubSeriesRead(name, SubSeriesState.NotCheckedOut);

        var sandbox = matches[0].Value;
        var read = await subSeries.ReadAsync(readerFactory.Create(sandbox), name, ticket, cancellationToken);
        if (read.State != SubSeriesState.Found) return read;
        return read with
        {
            Sha = await gitOps.GetLastCommitForPathsAsync(sandbox, SeriesReader.RevisionPaths(read.Base), cancellationToken),
        };
    }
}
