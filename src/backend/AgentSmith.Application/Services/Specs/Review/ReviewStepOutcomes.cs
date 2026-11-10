using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Specs;

namespace AgentSmith.Application.Services.Specs.Review;

/// <summary>
/// 2026-11-10-8e59: how the review step takes each spec's outcome — its step line, its objection, and the stop it sets.
/// The stop lands at the FIRST uncleared spec: a later one never moves it back, so no spec before it is built
/// unattacked, whichever order the walk and the series round's re-attacks found them in.
/// </summary>
internal static class ReviewStepOutcomes
{
    /// <summary>One spec's outcome on the step line, its objection, and its budget stop (returned, already set).</summary>
    public static string? Take(
        PipelineContext pipeline, SpecReviewOutcome outcome, List<string> lines, List<StandingObjection> objections)
    {
        lines.Add(Describe(outcome));
        if (StandingObjection.Of(outcome) is { } objection) objections.Add(objection);
        if (outcome.BudgetStop is { } stop) Stop(pipeline, outcome.Final.SpecId, stop, lines);
        return outcome.BudgetStop;
    }

    public static void Stop(PipelineContext pipeline, string specId, string why, List<string> lines)
    {
        var set = pipeline.Get<SpecSet>(ContextKeys.SpecSet);
        if (pipeline.TryGet<SpecSequenceStop>(ContextKeys.SpecSequenceStop, out var existing) && existing is not null
            && ReviewObjectionPark.IndexOf(set, existing.BeforeSpecId) <= ReviewObjectionPark.IndexOf(set, specId)) return;
        pipeline.Set(ContextKeys.SpecSequenceStop, new SpecSequenceStop(specId, why, Park: false));
        lines.Add($"stopped before {specId}, which is left to the next attempt with every spec after it: {why}");
    }

    private static string Describe(SpecReviewOutcome o) => o.StandingVerdict is null
        ? $"{o.Final.SpecId} cleared after {o.Rounds} round(s)"
        : $"{o.Final.SpecId}: {o.StandingVerdict} standing after {o.Rounds} round(s){(o.Unattacked ? ", its final text not attacked" : "")}";
}
