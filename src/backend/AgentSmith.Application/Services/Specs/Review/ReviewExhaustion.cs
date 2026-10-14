using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Specs.Review;

/// <summary>
/// 2026-10-13-8762h: which standing objections the author is asked about — decided once per review step, over
/// the spec objections and the series' together, from the ticket thread only. An objection whose
/// <see cref="ReviewObjectionComment.MarkerFor"/> line is on a comment of ours was asked: it proceeds, answered
/// or not, and an answer is read by the next derivation through its comment or ticket-edit cause. Every other
/// one is asked. A spec at or after an existing <see cref="ContextKeys.SpecSequenceStop"/> is never asked: it is
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
        var comments = pipeline.TryGet<IReadOnlyList<TicketComment>>(ContextKeys.TicketComments, out var c) ? c : null;
        var ask = new List<StandingObjection>();
        foreach (var objection in objections.Where(o => BeforeStop(o, pipeline)))
        {
            if (!ReviewObjectionComment.IsAsked(comments, objection.MarkerKey)) ask.Add(objection);
            else logger.LogInformation("The objection {Key} was asked before — it proceeds as reviewed", objection.MarkerKey);
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
