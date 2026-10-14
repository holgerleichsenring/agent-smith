using AgentSmith.Contracts.Commands;
using AgentSmith.Server.Models;

namespace AgentSmith.Server.Services.Passes;

/// <summary>
/// 2026-10-13-9a59f: prices the calls made outside every spec by the command of the step they ran in. A
/// step index can name two step rows after a park (the resume re-runs from the checkpointed count), so the
/// step is the row with that index and the largest trail position below the call's. A call with no step
/// index — or none that resolves — is not attributed.
/// </summary>
public static class RunCallAttribution
{
    public enum Bucket { Derive, Pr, Other, NotAttributed }

    public static Bucket Of(RunPassCall call, IReadOnlyList<RunPassStep> steps)
    {
        ArgumentNullException.ThrowIfNull(call);
        ArgumentNullException.ThrowIfNull(steps);
        if (call.StepIndex is not { } index) return Bucket.NotAttributed;
        var step = steps.Where(s => s.StepIndex == index
                && (call.EventSeq is null || s.EventSeq is null || s.EventSeq < call.EventSeq))
            .MaxBy(s => s.EventSeq ?? long.MinValue);
        if (step is null) return Bucket.NotAttributed;
        return Command(step.CommandName) switch
        {
            CommandNames.DeriveSpec => Bucket.Derive,
            CommandNames.CommitAndPR or CommandNames.PrCrossLink => Bucket.Pr,
            _ => Bucket.Other,
        };
    }

    // A typed command name may carry a ":param" suffix.
    private static string? Command(string? name) => name?.Split(':', 2)[0];

    public static (RunPassCostView Derive, RunPassCostView Pr, RunPassCostView Other, RunPassCostView NotAttributed)
        Price(IEnumerable<RunPassCall> calls, IReadOnlyList<RunPassStep> steps)
    {
        var totals = new Dictionary<Bucket, RunPassCostView>();
        foreach (var call in calls)
        {
            var bucket = Of(call, steps);
            totals[bucket] = totals.GetValueOrDefault(bucket, RunPassCostView.None).Add(call.CostUsd);
        }
        RunPassCostView Get(Bucket b) => totals.GetValueOrDefault(b, RunPassCostView.None);
        return (Get(Bucket.Derive), Get(Bucket.Pr), Get(Bucket.Other), Get(Bucket.NotAttributed));
    }
}
