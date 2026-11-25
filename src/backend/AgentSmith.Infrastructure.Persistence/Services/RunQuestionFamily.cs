using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Runs;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Persistence.Services;

/// <summary>
/// 2026-11-25-9992k: a question a run asked the operator (<c>ask_human</c>) is folded onto the run's metrics, so a model
/// comparison can read how often each model asked. The trail row is the fold's identity, as for a sandbox result: it is
/// written in the same SaveChanges, and an event whose row is there was folded already. Every other tool call is
/// trail-only.
/// </summary>
public sealed class RunQuestionFamily(RunMetricsProjection metrics) : IRunEventFamily
{
    public IReadOnlyCollection<Type> Events { get; } = [typeof(ToolCallEvent)];

    /// <summary>True for the tool call this family folds — the projector reserves its trail position instead of
    /// buffering it.</summary>
    public static bool Folds(AgentSmith.Contracts.Events.RunEvent runEvent) => runEvent is ToolCallEvent { Tool: RunMetrics.QuestionTool };

    public async Task ApplyAsync(IUnitOfWork uow, AgentSmith.Contracts.Events.RunEvent runEvent, long? eventSeq, CancellationToken ct)
    {
        if (runEvent is not ToolCallEvent { Tool: RunMetrics.QuestionTool } e) return;
        if (eventSeq is { } seq)
        {
            if (await uow.Set<Entities.RunEvent>().AnyAsync(t => t.RunId == e.RunId && t.Seq == seq, ct)) return;
            uow.Add(RunTrailRowMapper.Map(e.RunId, seq, e));
        }
        var run = await uow.Set<Run>().FirstOrDefaultAsync(r => r.Id == e.RunId, ct);
        if (run is not null && run.FinishedAt is null) metrics.Fold(run, e);
        await uow.SaveChangesAsync(ct);
    }
}
