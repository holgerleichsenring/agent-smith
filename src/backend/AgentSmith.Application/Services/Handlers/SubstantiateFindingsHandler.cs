using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Scans;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// p0429: nothing ships as CRITICAL on a scanner's regex and a master's silence.
/// <para>
/// A security scan once delivered nine false-positive criticals, every one of them
/// promoted because the master said nothing about it. This step is where that silence
/// stops being an answer: each unvouched finding is resolved against the source it cites
/// and put to a fresh instance asked to refute it.
/// </para>
/// </summary>
public sealed class SubstantiateFindingsHandler(
    IFindingSubstantiator substantiator,
    ILogger<SubstantiateFindingsHandler> logger)
    : ICommandHandler<SubstantiateFindingsContext>
{
    public async Task<CommandResult> ExecuteAsync(
        SubstantiateFindingsContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var before = Count(context.Pipeline);
        var delivered = await substantiator.SubstantiateAsync(context.Pipeline, cancellationToken);
        context.Pipeline.Set(ContextKeys.SkillObservations, delivered.ToList());

        var criticals = delivered.Count(o => o.Severity is ObservationSeverity.Critical);
        logger.LogInformation(
            "Substantiation: {After} of {Before} findings delivered, {Criticals} of them critical",
            delivered.Count, before, criticals);
        return CommandResult.Ok(
            $"Substantiated: {delivered.Count} of {before} findings delivered ({criticals} critical)");
    }

    private static int Count(PipelineContext pipeline) =>
        pipeline.TryGet<List<SkillObservation>>(ContextKeys.SkillObservations, out var o) && o is not null
            ? o.Count
            : 0;
}
