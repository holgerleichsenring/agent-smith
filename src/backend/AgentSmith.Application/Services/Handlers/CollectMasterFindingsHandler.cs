using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// p0267: routes the api-security-master's TRIAGED output into SkillObservations so
/// DeliverFindings stops reporting 0. The master emits its final answer as an
/// observation JSON array (the same shape the judge skills use); this step parses it
/// with the existing <see cref="ObservationParser"/> and appends to
/// ContextKeys.SkillObservations. Raw Nuclei/Spectral/ZAP results are NOT promoted —
/// only the master's triage reaches delivery. The scrape is gated on the master
/// skill's declared output_schema == observation, so a coding master (code + verdict)
/// is left untouched even if this step is ever wired into another preset.
/// </summary>
public sealed class CollectMasterFindingsHandler(
    IMasterOutputSchemaResolver schemaResolver,
    ObservationParser observationParser,
    ILogger<CollectMasterFindingsHandler> logger)
    : ICommandHandler<CollectMasterFindingsContext>
{
    private const string ObservationSchema = "observation";

    public Task<CommandResult> ExecuteAsync(
        CollectMasterFindingsContext context, CancellationToken cancellationToken)
    {
        var pipeline = context.Pipeline;
        var masterSkill = pipeline.TryGet<string>(ContextKeys.MasterSkillName, out var ms) ? ms : null;
        if (string.IsNullOrWhiteSpace(masterSkill))
            return Skip("no master skill ran");

        var schema = schemaResolver.Resolve(masterSkill);
        if (!string.Equals(schema, ObservationSchema, StringComparison.OrdinalIgnoreCase))
            return Skip($"master '{masterSkill}' declares output_schema '{schema ?? "none"}', not observation");

        if (!pipeline.TryGet<string>(ContextKeys.MasterAnswer, out var answer)
            || string.IsNullOrWhiteSpace(answer))
            return Skip($"master '{masterSkill}' produced no answer text");

        // p0279: pass the master's read-set so an analyzed_from_source claim on a file it
        // never read is downgraded to potential (honest evidence mode).
        pipeline.TryGet<List<string>>(ContextKeys.MasterReadPaths, out var readPaths);
        var observations = observationParser.TryParseWithoutIds(answer, masterSkill, logger, readPaths);
        if (observations is null || observations.Count == 0)
            return Skip($"master '{masterSkill}' answer held no parseable observations");

        ScannerObservationFactory.AppendObservations(pipeline, observations);
        logger.LogInformation(
            "Collected {Count} findings from master '{Skill}' into SkillObservations",
            observations.Count, masterSkill);
        return Task.FromResult(CommandResult.Ok(
            $"Collected {observations.Count} findings from '{masterSkill}'"));
    }

    private Task<CommandResult> Skip(string reason)
    {
        logger.LogInformation("CollectMasterFindings appended nothing — {Reason}", reason);
        return Task.FromResult(CommandResult.Ok($"No findings collected — {reason}"));
    }
}
