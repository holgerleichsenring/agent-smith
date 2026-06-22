using System.Text.Json;
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
    ObservationParser observationParser,
    ITolerantJsonParser tolerantParser,
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
            return Skip($"master '{masterSkill}' produced no answer text");

        // Empty-but-valid array (master triaged to nothing) vs unparseable (no usable
        // answer) collide in TryParseWithoutIds (both -> null), so detect array-ness
        // first: only a real JSON array enters the merge; anything else leaves the raw
        // scanner findings untouched (regression guard — never fewer than today).
        if (!IsJsonArray(answer))
            return Skip($"master '{masterSkill}' answer is not a JSON array — kept raw scanner findings");

        var raw = pipeline.TryGet<List<SkillObservation>>(ContextKeys.SkillObservations, out var existing)
            && existing is not null ? existing : [];
        pipeline.Set(ContextKeys.RawScannerObservations, raw.ToList());

        // p0279: anchor master source-claims against the read-set (downgrade unread ones).
        pipeline.TryGet<List<string>>(ContextKeys.MasterReadPaths, out var readPaths);
        var masterObs = observationParser.TryParseWithoutIds(answer, masterSkill, logger, readPaths)
            ?? new List<SkillObservation>();
        var merged = Merge(masterObs, raw);
        pipeline.Set(ContextKeys.SkillObservations, merged);

        var keptRaw = merged.Count - masterObs.Count;
        logger.LogInformation(
            "Merged master '{Skill}' triage ({Master}) + {KeptRaw} uncovered High+ scanner facts = {Total} delivered (from {Raw} raw)",
            masterSkill, masterObs.Count, keptRaw, merged.Count, raw.Count);
        return Task.FromResult(CommandResult.Ok(
            $"Merged: {masterObs.Count} triaged + {keptRaw} High+ raw = {merged.Count}"));
    }

    private bool IsJsonArray(string answer)
    {
        using var doc = tolerantParser.ParseArray(answer).Document;
        return doc is not null && doc.RootElement.ValueKind == JsonValueKind.Array;
    }

    // master-set + every High+ raw fact the master did not address at the same
    // (File, StartLine). Low/medium raw is dropped unless the master re-stated it.
    private static List<SkillObservation> Merge(
        IReadOnlyList<SkillObservation> master, IReadOnlyList<SkillObservation> raw)
    {
        var masterLocations = master
            .Where(HasLocation)
            .Select(o => (o.File!, o.StartLine))
            .ToHashSet();
        var result = new List<SkillObservation>(master);
        foreach (var r in raw)
        {
            if (!IsHighOrAbove(r.Severity)) continue;
            if (HasLocation(r) && masterLocations.Contains((r.File!, r.StartLine))) continue;
            result.Add(r);
        }
        return result;
    }

    private static bool HasLocation(SkillObservation o) =>
        !string.IsNullOrWhiteSpace(o.File) && o.StartLine > 0;

    private static bool IsHighOrAbove(ObservationSeverity s) =>
        s is ObservationSeverity.Critical or ObservationSeverity.High;

    private Task<CommandResult> Skip(string reason)
    {
        logger.LogInformation("MergeMasterFindings left raw findings unchanged — {Reason}", reason);
        return Task.FromResult(CommandResult.Ok($"No merge — {reason}"));
    }
}
