using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Passes;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Passes;

/// <summary>
/// 2026-10-13-9a59f: records the master passes of a spec. <see cref="Open"/> is synchronous so the
/// ambient pass frame it sets lives in the caller's flow — every model call and ledger flush of the pass
/// is stamped with it. <see cref="EndAsync"/> publishes one <see cref="MasterPassEndedEvent"/> per pass.
/// Only a spec step records: a scan or spec-dialog master has no spec in its step frame and opens nothing.
/// The run-wide ordinal and the ended-on-a-question flag are context keys, so a park carries them.
/// </summary>
public sealed class MasterPassRecorder(
    IEventPublisher events, IRunContextAccessor runContext, TimeProvider clock, ILogger<MasterPassRecorder> logger)
{
    /// <param name="reengageReason">Set for a re-engagement pass: it opens because of that reason.</param>
    public MasterPassFrame? Open(PipelineContext pipeline, int passInStep, string? reengageReason = null)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        var specId = runContext.CurrentSpecId;
        if (specId is null || !pipeline.TryGet<string>(ContextKeys.RunId, out var runId) || string.IsNullOrEmpty(runId))
            return null;
        var opened = reengageReason ?? Opener(pipeline);
        var ordinal = (pipeline.TryGet<int>(ContextKeys.MasterPassOrdinal, out var last) ? last : 0) + 1;
        pipeline.Set(ContextKeys.MasterPassOrdinal, ordinal);
        pipeline.Remove(ContextKeys.MasterPassEndedOnQuestion);
        return new MasterPassFrame(runId!, specId, ordinal, passInStep, opened, runContext.BeginMasterPassScope(ordinal));
    }

    public async Task EndAsync(
        PipelineContext pipeline, MasterPassFrame? pass, string endedBecause, string? reengageReason,
        MasterPassState state)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        if (pass is null) return;
        if (endedBecause == MasterPassReasons.Question) pipeline.Set(ContextKeys.MasterPassEndedOnQuestion, true);
        var ended = MasterPassEnd.Event(pass, endedBecause, reengageReason, state,
            MasterReengagementPolicy.RatifiedCriteria(pipeline), clock.GetUtcNow());
        try
        {
            // Not the step's token: a pass that ends on a cancel still leaves its record.
            await events.PublishAsync(ended, CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Master pass {Ordinal} of spec {SpecId} ended {Reason} — its record was not published",
                pass.Ordinal, pass.SpecId, endedBecause);
        }
    }

    // After an answer first: a master resumed inside a repair would otherwise read "repair" and lose it.
    internal static string Opener(PipelineContext pipeline) =>
        pipeline.Flag(ContextKeys.MasterPassEndedOnQuestion) ? MasterPassReasons.AfterAnswer
        : SpecReviewFixPass.InFlight(pipeline) ? MasterPassReasons.ReviewFix
        : pipeline.Has(ContextKeys.StructureFindings) ? MasterPassReasons.StructureCorrection
        : pipeline.Has(ContextKeys.OutstandingCriteria) ? MasterPassReasons.Repair
        : MasterPassReasons.First;
}
