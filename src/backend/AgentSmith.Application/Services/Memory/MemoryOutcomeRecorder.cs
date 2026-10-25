using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Memory;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Memory;

/// <summary>
/// 2026-10-25-9324c: at run end, what finally became of each spec becomes evidence for the memories it relied
/// on — done confirms (+1), failed strains (-0.5). Evaluated once, from the spec's FINAL standing, so a spec that
/// failed, was repaired and verified confirms and never strains. A spec handed back, found already satisfied or
/// never finished adds nothing. A failed write is logged; the run's record never fails for its evidence.
/// </summary>
public sealed class MemoryOutcomeRecorder(IMemoryEvidenceStore store, ILogger<MemoryOutcomeRecorder> logger)
{
    public async Task RecordAsync(PipelineContext pipeline, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        if (!pipeline.TryGet<IReadOnlyDictionary<string, IReadOnlyList<MemoryCitation>>>(ContextKeys.MemoryReliance, out var relied)
            || relied is not { Count: > 0 }
            || !pipeline.TryGet<SpecSequenceProgress>(ContextKeys.SpecSequenceProgress, out var progress) || progress is null
            || !pipeline.TryGet<string>(ContextKeys.RunId, out var runId) || string.IsNullOrEmpty(runId)) return;
        var project = pipeline.TryGet<string>(ContextKeys.ProjectName, out var p) ? p ?? string.Empty : string.Empty;
        var ticket = pipeline.TryGet<TicketId>(ContextKeys.TicketId, out var t) ? t?.Value ?? string.Empty : string.Empty;
        try
        {
            foreach (var spec in progress.Specs)
                if (Outcome(spec) is { } outcome && relied.TryGetValue(spec.SpecId, out var cited))
                    foreach (var memory in cited)
                        await store.UpsertOutcomeAsync(new MemoryEvidenceFact(memory.RepoKey, memory.Slug, outcome.Kind,
                            outcome.Weight, project, ticket, runId!, spec.SpecId, null, DateTimeOffset.UtcNow), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "The memory outcomes of run {RunId} were not recorded", runId);
        }
    }

    private static (string Kind, double Weight)? Outcome(SpecProgress spec) => spec.State switch
    {
        SpecRunState.Done when spec.Note != SelectSpecHandler.AlreadySatisfiedNote =>
            (MemoryEvidenceKinds.Confirmed, MemoryStandingWeights.Confirmed),
        SpecRunState.Failed => (MemoryEvidenceKinds.Strained, MemoryStandingWeights.Strained),
        _ => null,
    };
}
