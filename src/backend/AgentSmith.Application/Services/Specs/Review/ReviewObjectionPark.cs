using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Specs.Review;

/// <summary>
/// 2026-10-13-8762h: asks the objections <see cref="ReviewExhaustion"/> chose. The set is published with its
/// reviewed specs and the one question through <see cref="SpecReviewRevision"/>, so the branch carries what the
/// next run reads. The ticket parks at the EARLIEST asked spec: before any spec is built when that is the first
/// unbuilt spec or an objection is the series' own (SpecHandback spliced in behind the step); otherwise through a
/// parking <see cref="SpecSequenceStop"/>, so the specs before it are built first. A park never moves an existing
/// stop later; at equal position the park wins.
/// </summary>
public sealed class ReviewObjectionPark(SpecReviewRevision revision, ILogger<ReviewObjectionPark> logger)
{
    public const string Cause = "spec review: a standing objection asked once";

    /// <summary><paramref name="done"/> when nothing is asked; else the step's result with the park.</summary>
    public async Task<CommandResult> ApplyAsync(
        PipelineContext pipeline, IReadOnlyList<StandingObjection> ask, CommandResult done, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(ask);
        ArgumentNullException.ThrowIfNull(done);
        if (ask.Count == 0) return done;
        var published = await revision.PublishAsync(pipeline, [], Cause, ct, ReviewObjectionQuestion.For(ask));
        if (!published.IsSuccess) return published;
        var set = pipeline.Get<SpecSet>(ContextKeys.SpecSet);
        var asked = $"{done.Message}; asked the author once: {string.Join(", ", ask.Select(o => o.MarkerKey))}";
        var at = ask.Any(o => o.SpecId is null) ? null : ask.Select(o => o.SpecId!).MinBy(id => IndexOf(set, id));
        if (at is null || at == set.UnexecutedTail.FirstOrDefault()?.SpecId)
        {
            logger.LogInformation("Review objection asked — parking before any spec is built: {Keys}", asked);
            return CommandResult.OkAndContinueWith($"{asked}; parked before any spec is built",
                PipelineCommand.Simple(CommandNames.SpecHandback));
        }
        var existing = pipeline.TryGet<SpecSequenceStop>(ContextKeys.SpecSequenceStop, out var s) ? s : null;
        if (existing is null || IndexOf(set, existing.BeforeSpecId) >= IndexOf(set, at))
            pipeline.Set(ContextKeys.SpecSequenceStop, new SpecSequenceStop(at,
                $"{ReviewObjectionComment.MarkerFor(at)}; the author is asked once", Park: true));
        logger.LogInformation("Review objection asked — the specs before {SpecId} are built first", at);
        return CommandResult.Ok($"{asked}; the specs before {at} are built, then the ticket parks there");
    }

    /// <summary>The spec's position in the set; past the end when the set does not carry it.</summary>
    internal static int IndexOf(SpecSet set, string specId)
    {
        var index = set.Specs.ToList().FindIndex(spec => spec.SpecId == specId);
        return index < 0 ? int.MaxValue : index;
    }
}
