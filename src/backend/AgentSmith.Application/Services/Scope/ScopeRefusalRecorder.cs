using AgentSmith.Application.Extensions;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Scope;

/// <summary>
/// What a scope-call refusal does to the run. The judgement was the model's; the
/// consequence is mechanical and lives here: the hand-back is set on the context
/// (only SpecSetPublisher sets it otherwise, and a refusal never reaches derivation),
/// recorded as a scope decision, and the hand-back step is spliced in directly behind
/// ScopeRepos so its park ends the run before the next step boots a sandbox.
/// DropAhead is phase-scoped and cannot retire pipeline-level steps, hence the splice.
/// </summary>
public sealed class ScopeRefusalRecorder(ILogger<ScopeRefusalRecorder> logger)
{
    public CommandResult Apply(PipelineContext pipeline, ScopeRefusal refusal)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(refusal);
        var record = $"Refused: {refusal.Reason} — quoted: \"{refusal.Quote}\"";
        pipeline.Set(ContextKeys.SpecHandback,
            new SpecHandback(SpecHandbackCase.Refused, refusal.Reason, refusal.Quote));
        pipeline.AppendDecisions([new PlanDecision("scope", record)]);
        logger.LogWarning("{Record}", record);
        return CommandResult.OkAndContinueWith(
            record, PipelineCommand.Simple(CommandNames.SpecHandback));
    }
}
