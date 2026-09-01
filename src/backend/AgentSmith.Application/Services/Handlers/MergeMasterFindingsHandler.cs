using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// p0277: routes the security-master's TRIAGED output into delivery without losing the
/// deterministic scanners' hard evidence. REFINE-WITH-SAFETY-NET: SkillObservations
/// becomes the master's curated observation array PLUS every uncovered High+ raw
/// scanner fact (static-pattern / git-history secret / dependency CVE). The master may
/// dedup, recategorize, add analysis findings and suppress low/medium noise — but a
/// High+ deterministic fact it does not address at the same location ships verbatim.
/// Gated on output_schema == observation, so the coding path is untouched.
/// </summary>
public sealed class MergeMasterFindingsHandler(
    IMasterOutputSchemaResolver schemaResolver,
    MasterAnswerReader answerReader,
    ILogger<MergeMasterFindingsHandler> logger)
    : ICommandHandler<MergeMasterFindingsContext>
{
    private const string ObservationSchema = "observation";

    public Task<CommandResult> ExecuteAsync(
        MergeMasterFindingsContext context, CancellationToken cancellationToken)
    {
        var pipeline = context.Pipeline;
        var masterSkill = pipeline.TryGet<string>(ContextKeys.MasterSkillName, out var ms) ? ms : null;
        if (string.IsNullOrWhiteSpace(masterSkill))
            return Skip("no master skill ran");

        var schema = schemaResolver.Resolve(masterSkill);
        if (!string.Equals(schema, ObservationSchema, StringComparison.OrdinalIgnoreCase))
            return Skip($"master '{masterSkill}' output_schema '{schema ?? "none"}' is not observation");

        if (!pipeline.TryGet<string>(ContextKeys.MasterAnswer, out var answer)
            || string.IsNullOrWhiteSpace(answer))
            return Degraded(pipeline, $"master '{masterSkill}' produced no answer text");

        // p0279: anchor master source-claims against the read-set (downgrade unread ones).
        // p0333: the same read-set lets the merge treat a static-pattern fact in a file the
        // master read-and-dismissed as an implicit rejection instead of an uncovered gap.
        pipeline.TryGet<List<string>>(ContextKeys.MasterReadPaths, out var readPaths);
        // 2026-09-01-6c32: the reader decides, because it is the code that can read a
        // truncated array. An empty-but-valid array is a triage that kept nothing and
        // enters the merge; only an answer that is not findings at all degrades.
        var reading = answerReader.Read(answer, masterSkill, logger, readPaths);
        if (reading.Rejection is not null)
            return Degraded(pipeline,
                $"master '{masterSkill}' {reading.Rejection} — kept raw scanner findings");
        if (reading.Recovered) RecordRecovery(pipeline, masterSkill, reading.Observations.Count);

        var raw = pipeline.TryGet<List<SkillObservation>>(ContextKeys.SkillObservations, out var existing)
            && existing is not null ? existing : [];
        pipeline.Set(ContextKeys.RawScannerObservations, raw.ToList());

        var masterObs = reading.Observations;
        var merge = MasterFindingsMerger.Merge(masterObs, raw, readPaths);
        pipeline.Set(ContextKeys.SkillObservations, merge.Delivered.ToList());
        // p0429: the master's silence promoted these; nobody vouched for them. Named here
        // so SubstantiateFindings can ask a fresh instance to refute each one.
        pipeline.Set(ContextKeys.UnvouchedFindings, merge.Promoted.ToList());

        logger.LogInformation(
            "Merged master '{Skill}' triage ({Master}) + {KeptRaw} uncovered High+ scanner facts = {Total} delivered (from {Raw} raw, {Suppressed} static-pattern facts suppressed as master-reviewed)",
            masterSkill, masterObs.Count, merge.Promoted.Count, merge.Delivered.Count, raw.Count,
            merge.SuppressedAsReviewed);
        return Task.FromResult(CommandResult.Ok(
            $"Merged: {masterObs.Count} triaged + {merge.Promoted.Count} High+ raw = {merge.Delivered.Count}"));
    }

    /// <summary>
    /// 2026-09-01-6c32: the array was cut off mid-write and its observations were salvaged
    /// one literal at a time. They ship — they are the master's own triage — but the run
    /// records the salvage, so a reader can tell a complete triage from a rescued one.
    /// </summary>
    private void RecordRecovery(PipelineContext pipeline, string masterSkill, int count)
    {
        var note = $"master '{masterSkill}' answer was truncated — {count} observation(s) "
            + "recovered from the incomplete array";
        pipeline.Set(ContextKeys.ScanTriageRecovered, note);
        logger.LogWarning("Scan triage recovered from a truncated answer — {Note}", note);
    }

    /// <summary>
    /// 2026-08-30-03e4: the master ran under the observation schema and owed a triage, and
    /// what came back could not be read — so the delivered set is raw scanner output. The
    /// reason is recorded on the run because nothing downstream can tell this apart from a
    /// thorough scan: three identical runs delivered 25, 26 and 37 findings and the
    /// untriaged one looked like the thorough one. The two branches where no triage was
    /// ever owed (no master ran; the master is not an observation master) stay a plain Skip.
    /// </summary>
    private Task<CommandResult> Degraded(PipelineContext pipeline, string reason)
    {
        pipeline.Set(ContextKeys.ScanTriageDegraded, reason);
        return Skip(reason);
    }

    private Task<CommandResult> Skip(string reason)
    {
        logger.LogInformation("MergeMasterFindings left raw findings unchanged — {Reason}", reason);
        return Task.FromResult(CommandResult.Ok($"No merge — {reason}"));
    }
}
