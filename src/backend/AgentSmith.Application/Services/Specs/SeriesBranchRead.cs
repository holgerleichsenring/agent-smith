using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Specs;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-10-12-f5f7d: reads the ticket's series off every run repository for DeriveSpec and says
/// where its next revision may be written. The repository a legacy spec belongs to is the pointer's,
/// else the approval's, else the run's only one — never the first of the scope. A read with lagging
/// members is REFUSED naming them: until a later slice hands it back, nothing is derived from or
/// written over a series whose repositories disagree. The ids a new spec must not take are put on
/// the run before the model is asked.
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
        if (onBranch.Read is { Lagging.Count: > 0 } read)
            return SeriesBranch.Refused(onBranch,
                $"The series of {key.Value} is out of step: {string.Join(", ", read.Lagging)} "
                + "lag the newest revision, so no revision is derived or written until they agree");
        pipeline.Set(ContextKeys.SpecTakenIds,
            await takenIds.ReadAsync(pipeline, repos, pointer, onBranch.Read?.Set ?? approval?.Set, cancellationToken));
        var members = onBranch.Read is { } found ? [.. found.RevisionShas.Keys] : (IReadOnlyList<string>)[];
        return new SeriesBranch(onBranch, new SeriesScope(repos, legacy, members, pointer?.CarryingRepo));
    }
}
