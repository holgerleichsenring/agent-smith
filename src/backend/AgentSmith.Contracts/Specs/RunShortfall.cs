using AgentSmith.Contracts.Commands;
using AgentSmith.Domain.Models;

namespace AgentSmith.Contracts.Specs;

/// <summary>
/// p0439: a run that stopped after at least one phase was built, verified and recorded
/// done, with at least one phase still open. It falls short of the ratified contract and
/// it DELIVERS — the verified phases on a ready pull request, the open ones named with
/// the reason the run stopped. It is not <c>failed</c> (the verified work is sound) and
/// not <c>cancelled</c> (nobody stopped it); it is a done with a shortfall.
/// </summary>
/// <param name="Delivered">The phases verification recorded done, in sequence order.</param>
/// <param name="NotDelivered">The phases that never reached done, in sequence order.</param>
/// <param name="Reason">Why the run stopped — the failed step's own words.</param>
public sealed record RunShortfall(
    IReadOnlyList<PhaseProgress> Delivered,
    IReadOnlyList<PhaseProgress> NotDelivered,
    string Reason)
{
    public int PhaseCount => Delivered.Count + NotDelivered.Count;

    /// <summary>One line for the run's own result and log.</summary>
    public string Summary =>
        $"Delivered {Delivered.Count} of {PhaseCount} phase(s); not delivered: "
        + $"{string.Join(", ", NotDelivered.Select(p => p.PhaseId))} — {Reason}";

    /// <summary>
    /// The shortfall a stopped run WOULD deliver: null unless the run stopped for a reason,
    /// a phase is done and a phase is not. A run whose first phase failed has nothing
    /// verified to deliver; a run whose every phase is done did not fall short of its
    /// contract, whatever failed afterwards.
    /// </summary>
    public static RunShortfall? Of(SpecSequenceProgress? progress, string? reason)
    {
        if (progress is null || string.IsNullOrWhiteSpace(reason)) return null;
        var delivered = progress.Phases.Where(p => p.State == PhaseRunState.Done).ToList();
        var notDelivered = progress.Phases.Where(p => p.State != PhaseRunState.Done).ToList();
        return delivered.Count == 0 || notDelivered.Count == 0
            ? null
            : new RunShortfall(delivered, notDelivered, reason.Trim());
    }

    /// <summary>Read off the run: the failure an earlier step left and the phase table.</summary>
    public static RunShortfall? Of(PipelineContext pipeline)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        var progress = pipeline.TryGet<SpecSequenceProgress>(ContextKeys.SpecSequenceProgress, out var p) ? p : null;
        var reason = pipeline.TryGet<string>(ContextKeys.FailureReason, out var r) ? r : null;
        return Of(progress, reason);
    }

    /// <summary>
    /// The shortfall CommitAndPR actually delivered — pull request opened, ticket finalized.
    /// Null on every other run, including a shortfall whose delivery did not happen.
    /// </summary>
    public static RunShortfall? DeliveredOn(PipelineContext pipeline)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        return pipeline.TryGet<RunShortfall>(ContextKeys.RunShortfall, out var delivered) ? delivered : null;
    }

    public void MarkDelivered(PipelineContext pipeline)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        pipeline.Set(ContextKeys.RunShortfall, this);
    }
}
