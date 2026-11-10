using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Specs.Review;

/// <summary>
/// 2026-10-13-8762h: which standing objections the author is asked about — decided once per review step, over
/// the spec objections and the series' together, from the ticket thread only. 2026-11-10-8e59: an objection proceeds
/// only when a person answered it with the build reading (<see cref="ReviewObjectionComment.BuildAsReviewedAnswered"/>);
/// every other one is asked, again if it was asked before — silence is never consent, and the fresh rounds that
/// led here already attacked the text again. A spec at or after an existing <see cref="ContextKeys.SpecSequenceStop"/> is never asked: it is
/// not built in this attempt, and a park there would move the stop later. The run-scoped question pin is not
/// the basis — it is set for any unanswered question and lives one run.
/// </summary>
public sealed class ReviewExhaustion(ILogger<ReviewExhaustion> logger)
{
    /// <summary>The objections to ask; empty means proceed.</summary>
    public IReadOnlyList<StandingObjection> Decide(IReadOnlyList<StandingObjection> objections, PipelineContext pipeline)
    {
        ArgumentNullException.ThrowIfNull(objections);
        ArgumentNullException.ThrowIfNull(pipeline);
        var ask = new List<StandingObjection>();
        foreach (var objection in objections.Where(o => BeforeStop(o, pipeline)))
        {
            // 2026-11-10-8e59: only a person's explicit build answer lets it through; asked and silent is asked again.
            if (!ReviewObjectionComment.BuildAsReviewedAnswered(pipeline, objection.MarkerKey)) ask.Add(objection);
            else logger.LogInformation("The author answered build as reviewed for {Key} — it is built", objection.MarkerKey);
        }
        return ask;
    }

    private static bool BeforeStop(StandingObjection objection, PipelineContext pipeline)
    {
        if (objection.SpecId is null
            || !pipeline.TryGet<SpecSequenceStop>(ContextKeys.SpecSequenceStop, out var stop) || stop is null) return true;
        var set = pipeline.Get<SpecSet>(ContextKeys.SpecSet);
        return ReviewObjectionPark.IndexOf(set, objection.SpecId) < ReviewObjectionPark.IndexOf(set, stop.BeforeSpecId);
    }
}
