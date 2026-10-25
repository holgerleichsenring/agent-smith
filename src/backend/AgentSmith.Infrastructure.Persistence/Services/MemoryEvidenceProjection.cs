using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Memory;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using RunEvent = AgentSmith.Contracts.Events.RunEvent;

namespace AgentSmith.Infrastructure.Persistence.Services;

/// <summary>
/// 2026-10-25-9324b: the memory-use family — a spec's relied list REPLACES that spec's relied rows (hand_forward's
/// last call wins), and a dispute appends one contradiction, written once per trail position.
/// </summary>
public sealed class MemoryEvidenceProjection(ProjectedEventRecords records, MemoryEvidenceWriter writer) : IRunEventFamily
{
    public IReadOnlyCollection<Type> Events { get; } = [typeof(MemoryUseRecordedEvent)];

    public async Task ApplyAsync(IUnitOfWork uow, RunEvent runEvent, long? eventSeq, CancellationToken ct)
    {
        if (runEvent is not MemoryUseRecordedEvent e) return;
        if (e.Disputed is { } disputed)
        {
            if (await records.HoldsAsync<MemoryEvidence>(uow, e.RunId, eventSeq, ct)) return;
            await writer.AppendAsync(uow, Fact(e, disputed, MemoryEvidenceKinds.Contradicted,
                MemoryStandingWeights.Disputed, e.Reason, eventSeq), ct);
            return;
        }
        if (e.SpecId is not { } specId) return;
        await writer.ReplaceAsync(uow, e.RunId, specId, MemoryEvidenceKinds.Relied,
            [.. e.Relied.Select(c => Fact(e, c, MemoryEvidenceKinds.Relied, MemoryStandingWeights.Relied, null, eventSeq))], ct);
    }

    private static MemoryEvidenceFact Fact(
        MemoryUseRecordedEvent e, MemoryCitation cited, string kind, double weight, string? reason, long? eventSeq) =>
        new(cited.RepoKey, cited.Slug, kind, weight, e.Project, e.TicketId, e.RunId, e.SpecId ?? string.Empty, reason,
            e.Timestamp, eventSeq);
}
