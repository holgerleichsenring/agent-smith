using AgentSmith.Contracts.Events;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Persistence.Services;

/// <summary>
/// p0404: attributes the time a run spends to the STEP that spent it, split out of
/// <see cref="RunEventApplier"/> like <see cref="RunCheckpointProjection"/>. Model
/// time, its throttle share and sandbox wall time are already measured per call —
/// what was missing is a durable home for them per step, so a finished run can
/// still answer where its wall-clock went instead of reporting zero forever.
/// <para>
/// The remainder (step duration minus model minus sandbox) is deliberately NOT
/// stored: it is a subtraction the read path makes from the step's own duration,
/// and storing it would let the two disagree.
/// </para>
/// </summary>
public sealed class RunStepTimeProjection
{
    public Task FoldLlmAsync(IUnitOfWork uow, LlmCallFinishedEvent e, CancellationToken ct) =>
        FoldAsync(uow, e.RunId, e.OriginStepIndex, step =>
        {
            step.LlmMs += e.DurationMs;
            step.ThrottleWaitMs += e.ThrottleWaitMs;
        }, ct);

    public Task FoldSandboxAsync(IUnitOfWork uow, SandboxResultEvent e, CancellationToken ct) =>
        FoldAsync(uow, e.RunId, e.OriginStepIndex, step => step.SandboxMs += e.DurationMs, ct);

    // An event without a step stamp (raised outside any step, or by a producer
    // that predates p0388a) is left unattributed rather than charged to a guess.
    // The caller owns the SaveChanges — the applier already saves on the same
    // unit of work, and a second save would be a second round trip per event.
    private static async Task FoldAsync(
        IUnitOfWork uow, string runId, int? stepIndex, Action<RunStep> fold, CancellationToken ct)
    {
        if (stepIndex is not { } index) return;
        // The applier can leave a second row behind for one index; the LATEST row
        // is the one the read path serves, so it is the one that must carry the time.
        var step = await uow.Set<RunStep>()
            .Where(s => s.RunId == runId && s.StepIndex == index)
            .OrderByDescending(s => s.Id).FirstOrDefaultAsync(ct);
        if (step is null) return;
        fold(step);
    }
}
