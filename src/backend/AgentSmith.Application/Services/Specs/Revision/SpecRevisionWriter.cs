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
/// 2026-11-27-6625: the set's member takes the revision's repos before the publish, so placement writes it where the
/// revision says and CurrentSpec — read by the write guard and the spec's checks — carries them.
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
        var was = before.Specs.First(s => s.SpecId == text.SpecId).Draft.Repos;
        pipeline.Set(ContextKeys.SpecSet, before with
        {
            StandIns = [.. before.StandIns.Where(id => id != text.SpecId)],
            Specs = [.. before.Specs.Select(s => s.SpecId == text.SpecId ? s with { Draft = s.Draft with { Repos = text.Repos } } : s)],
        });
        if (await PublishAsync(pipeline, before, [text], Cause(pipeline, reason), ct) is { } refused) return refused;
        var number = pipeline.Get<SpecSet>(ContextKeys.SpecSet).Current.Number;
        var spec = SpecCriteriaReset.Apply(pipeline, text.SpecId);
        await RecordAsync(pipeline, spec, number, reason, ct);
        return SpecRevisionReplies.Written(spec, number, Moved(was, spec.Repos) ? was : null);
    }

    /// <summary>
    /// 2026-11-27-3532d: writes a retirement — <paramref name="retired"/> is the set <see cref="SpecRetirement"/> left,
    /// published as one revision caused "retired in run &lt;id&gt;: &lt;reason&gt;". The replacement becomes the spec the
    /// run builds, so the rest of the retired spec's block verifies and records it; the retired spec's accounts are
    /// dropped, its row is superseded and the replacement's row stands behind it.
    /// </summary>
    public async Task<string> RetireAsync(
        PipelineContext pipeline, SpecSet retired, SpecDraft replacement, string retiredId, string reason, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(retired);
        ArgumentNullException.ThrowIfNull(replacement);
        var before = pipeline.Get<SpecSet>(ContextKeys.SpecSet);
        var was = before.Specs.First(s => s.SpecId == retiredId).Draft.Repos;
        pipeline.Set(ContextKeys.SpecSet, retired);
        if (await PublishAsync(pipeline, before, [], RetiredCause(pipeline, retiredId, replacement.SpecId, reason), ct) is { } refused)
            return refused;
        var number = pipeline.Get<SpecSet>(ContextKeys.SpecSet).Current.Number;
        pipeline.Set(ContextKeys.RunAccounts, RunAccountLedger.Current(pipeline).Without(retiredId));
        var spec = SpecCriteriaReset.Apply(pipeline, replacement.SpecId);
        var note = $"superseded by {spec.SpecId} in revision {number}: {Clean(reason)}";
        var table = pipeline.TryGet<SpecSequenceProgress>(ContextKeys.SpecSequenceProgress, out var held) && held is not null
            ? held : SpecSequenceProgress.ForSet(before);
        pipeline.Set(ContextKeys.SpecSequenceProgress, table.Superseding(retiredId, spec.SpecId, spec.Goal, note));
        await progress.RecordAsync(pipeline, retiredId, SpecRunState.Superseded, note: note, cancellationToken: ct);
        await progress.RecordAsync(pipeline, spec.SpecId, SpecRunState.InProgress,
            note: $"replaces {retiredId}: {Clean(reason)}", cancellationToken: ct);
        await decisions.LogAsync(null, DecisionCategory.Implementation,
            $"Spec {retiredId} retired in run {RunId(pipeline)}, superseded by {spec.SpecId} (revision {number}): {Clean(reason)}", ct, Source);
        logger.LogInformation("Spec {Retired} retired by its master for {Replacement} in revision {Number}: {Reason}",
            retiredId, spec.SpecId, number, reason);
        return SpecRevisionReplies.Retired(retiredId, spec, number, Moved(was, spec.Repos) ? was : null);
    }

    /// <summary>2026-11-27-3532d: a retirement's cause: the run, the retired spec, its replacement and why.</summary>
    public static string RetiredCause(PipelineContext pipeline, string retiredId, string replacementId, string reason) =>
        $"retired in run {RunId(pipeline)}: {retiredId} superseded by {replacementId}: {Clean(reason)}";

    // 2026-11-27-6625: placement may move the carrying repository with the spec; a failed write puts it and the set back.
    private async Task<string?> PublishAsync(
        PipelineContext pipeline, SpecSet before, IReadOnlyList<SpecDraft> texts, string cause, CancellationToken ct)
    {
        var carrier = pipeline.TryGet<string>(ContextKeys.SpecRepo, out var held) ? held : null;
        var published = await revision.PublishAsync(pipeline, texts, cause, ct);
        if (published.IsSuccess) return null;
        pipeline.Set(ContextKeys.SpecSet, before);
        if (carrier is null) pipeline.Remove(ContextKeys.SpecRepo);
        else pipeline.Set(ContextKeys.SpecRepo, carrier);
        return SpecRevisionReplies.NotRevised(published.Message);
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

    private static bool Moved(IReadOnlyList<string> was, IReadOnlyList<string> now) =>
        !was.ToHashSet(StringComparer.OrdinalIgnoreCase).SetEquals(now);

    private static string RunId(PipelineContext pipeline) =>
        pipeline.TryGet<string>(ContextKeys.RunId, out var id) && !string.IsNullOrEmpty(id) ? id! : "(no run id)";

    private static string Clean(string reason) => reason.Replace('\n', ' ').Replace('\r', ' ').Trim();
}
