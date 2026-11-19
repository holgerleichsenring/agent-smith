using AgentSmith.Application.Services.Specs.Review;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Decisions;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Specs;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Specs.Revision;

/// <summary>
/// 2026-11-17-ec48a: writes the coding master's revision through the one in-run revision publisher
/// (<see cref="SpecReviewRevision"/>), under the cause "revised in run &lt;id&gt;: &lt;reason&gt;" — so the branch reads
/// "revision N (revised in run &lt;id&gt;: &lt;reason&gt;)". The run's decision log and the spec's row record it, and
/// the spec's criteria become the revised ones. A write the branch did not take leaves the run's set as it was.
/// 2026-11-19-3e10: the master's text is written as it is; no review round is held or stored.
/// </summary>
public sealed class SpecRevisionWriter(
    SpecReviewRevision revision, IDecisionLogger decisions, ISpecProgressRecorder progress, ILogger<SpecRevisionWriter> logger)
{
    public const string Source = "revise-spec";

    public async Task<string> WriteAsync(PipelineContext pipeline, SpecDraft text, string reason, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(text);
        var before = pipeline.Get<SpecSet>(ContextKeys.SpecSet);
        // 2026-11-19-6b0ba: a written revision is the spec its master wrote — no longer the ticket's stand-in.
        pipeline.Set(ContextKeys.SpecSet, before with { StandIns = [.. before.StandIns.Where(id => id != text.SpecId)] });
        var published = await revision.PublishAsync(pipeline, [text], Cause(pipeline, reason), ct);
        if (!published.IsSuccess)
        {
            pipeline.Set(ContextKeys.SpecSet, before);
            return SpecRevisionReplies.NotRevised(published.Message);
        }
        var number = pipeline.Get<SpecSet>(ContextKeys.SpecSet).Current.Number;
        var spec = SpecCriteriaReset.Apply(pipeline, text.SpecId);
        await RecordAsync(pipeline, spec, number, reason, ct);
        return SpecRevisionReplies.Written(spec, number);
    }

    /// <summary>The revision's cause: the run that revised the spec and why.</summary>
    public static string Cause(PipelineContext pipeline, string reason) =>
        $"revised in run {RunId(pipeline)}: {Clean(reason)}";

    private async Task RecordAsync(PipelineContext pipeline, SpecDraft spec, int number, string reason, CancellationToken ct)
    {
        await decisions.LogAsync(null, DecisionCategory.Implementation,
            $"Spec {spec.SpecId} revised in run {RunId(pipeline)} as revision {number}: {Clean(reason)}", ct, Source);
        await progress.RecordAsync(pipeline, spec.SpecId, SpecRunState.InProgress,
            note: $"revision {number}: {Clean(reason)}", cancellationToken: ct);
        logger.LogInformation("Spec {Spec} revised by its master as revision {Number}: {Reason}", spec.SpecId, number, reason);
    }

    private static string RunId(PipelineContext pipeline) =>
        pipeline.TryGet<string>(ContextKeys.RunId, out var id) && !string.IsNullOrEmpty(id) ? id! : "(no run id)";

    private static string Clean(string reason) => reason.Replace('\n', ' ').Replace('\r', ' ').Trim();
}
