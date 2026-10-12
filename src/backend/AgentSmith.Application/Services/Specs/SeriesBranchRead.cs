using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Specs;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-10-12-f5f7d: reads the ticket's series off every run repository for DeriveSpec and says
/// where its next revision may be written. The repository a legacy spec belongs to is the pointer's,
/// else the approval's, else the run's only one — never the first of the scope. The ids a new spec must
/// not take are put on the run before the model is asked. 2026-10-12-f5f7f: a read with lagging members
/// is no longer refused here — the source decision hands it back; its members include the lagging ones,
/// so a re-cut is written into every repository of the series.
/// </summary>
public sealed class SeriesBranchRead(ISpecSetReader reader, SeriesTakenIds takenIds)
{
    public async Task<SeriesBranch> ReadAsync(
        PipelineContext pipeline, IReadOnlyList<RepoConnection> repos, TicketKey key,
        SpecSetPointer? pointer, SpecApprovalRecord? approval, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(repos);
        var legacy = SeriesScope.LegacyOf(pointer, approval?.CarryingRepo, repos);
        var carrying = repos.FirstOrDefault(r => string.Equals(r.Name, legacy, StringComparison.OrdinalIgnoreCase))
            ?? new RepoConnection { Name = legacy ?? string.Empty };
        var onBranch = await reader.ReadAsync(pipeline, carrying, repos, key, cancellationToken);
        pipeline.Set(ContextKeys.SpecTakenIds,
            await takenIds.ReadAsync(pipeline, repos, pointer, onBranch.Read?.Set ?? approval?.Set, cancellationToken));
        var members = onBranch.Read is { } found ? [.. found.RevisionShas.Keys, .. found.Lagging] : (IReadOnlyList<string>)[];
        return new SeriesBranch(onBranch, new SeriesScope(repos, legacy, members, pointer?.CarryingRepo));
    }
}
