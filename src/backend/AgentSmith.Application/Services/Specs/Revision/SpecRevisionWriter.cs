using AgentSmith.Application.Services.Specs.Review;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Decisions;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Specs;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Specs.Revision;

/// <summary>
/// 2026-11-17-ec48a: writes a revision the coding master's review cleared, through the one in-run revision publisher
/// (<see cref="SpecReviewRevision"/>), under the cause "revised in run &lt;id&gt;: &lt;reason&gt;" — so the branch reads
/// "revision N (revised in run &lt;id&gt;: &lt;reason&gt;)". The review round is stored with that number, the run's
/// decision log and the spec's row record it, and the spec's criteria become the revised ones. A write the branch did
/// not take leaves the run's set as it was.
/// </summary>
public sealed class SpecRevisionWriter(
    SpecReviewRevision revision, SpecReviewRoundPublisher rounds, IDecisionLogger decisions, ISpecProgressRecorder progress,
    ILogger<SpecRevisionWriter> logger)
{
    public const string Source = "revise-spec";

    public async Task<string> WriteAsync(
        SpecReviewRun run, SpecDraft text, string reason, SpecRevisionVerdict? verdict, int round, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(text);
        var before = run.Pipeline.Get<SpecSet>(ContextKeys.SpecSet);
        // 2026-11-19-6b0ba: a written revision is the spec its master wrote — no longer the ticket's stand-in.
        run.Pipeline.Set(ContextKeys.SpecSet, before with { StandIns = [.. before.StandIns.Where(id => id != text.SpecId)] });
        var published = await revision.PublishAsync(run.Pipeline, [text], Cause(run.Pipeline, reason), ct);
        if (!published.IsSuccess)
        {
            run.Pipeline.Set(ContextKeys.SpecSet, before);
            await RoundAsync(text.SpecId, verdict, round, null, ct);
            return SpecRevisionReplies.NotRevised(published.Message);
        }
        var number = run.Pipeline.Get<SpecSet>(ContextKeys.SpecSet).Current.Number;
        await RoundAsync(text.SpecId, verdict, round, number, ct);
        var spec = SpecCriteriaReset.Apply(run.Pipeline, text.SpecId);
        await RecordAsync(run.Pipeline, spec, number, reason, verdict, ct);
        return SpecRevisionReplies.Written(spec, number, verdict);
    }

    /// <summary>The review round, stored like a pre-build round; nothing when no review ran.</summary>
    public Task RoundAsync(string specId, SpecRevisionVerdict? verdict, int round, int? number, CancellationToken ct) =>
        verdict is null || verdict.Answers.Count == 0
            ? Task.CompletedTask
            : rounds.PublishAsync(new SpecReviewRound(specId, SpecReviewScopes.Spec, round, verdict.Evidence.Problems, [],
                number), verdict.Answers, ct);

    /// <summary>The revision's cause: the run that revised the spec and why.</summary>
    public static string Cause(PipelineContext pipeline, string reason) =>
        $"revised in run {RunId(pipeline)}: {Clean(reason)}";

    private async Task RecordAsync(
        PipelineContext pipeline, SpecDraft spec, int number, string reason, SpecRevisionVerdict? verdict, CancellationToken ct)
    {
        var review = verdict?.Merged?.Verdict ?? "none, spec_review.rounds is 0";
        await decisions.LogAsync(null, DecisionCategory.Implementation,
            $"Spec {spec.SpecId} revised in run {RunId(pipeline)} as revision {number} (review: {review}): {Clean(reason)}",
            ct, Source);
        await progress.RecordAsync(pipeline, spec.SpecId, SpecRunState.InProgress,
            note: $"revision {number}: {Clean(reason)}", cancellationToken: ct);
        logger.LogInformation("Spec {Spec} revised by its master as revision {Number}: {Reason}", spec.SpecId, number, reason);
    }

    private static string RunId(PipelineContext pipeline) =>
        pipeline.TryGet<string>(ContextKeys.RunId, out var id) && !string.IsNullOrEmpty(id) ? id! : "(no run id)";

    private static string Clean(string reason) => reason.Replace('\n', ' ').Replace('\r', ' ').Trim();
}
