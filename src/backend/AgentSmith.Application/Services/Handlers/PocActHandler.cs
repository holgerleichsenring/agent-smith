using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Pocs;
using AgentSmith.Application.Services.Pocs.Acts;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Exceptions;
using AgentSmith.Contracts.Pocs;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// 2026-11-23-c81ed: the first step of a lifecycle act — takes the act from the run's preset, re-checks its precondition
/// against the Poc row as it stands now (<see cref="PocActPlan"/>, the table the launcher read), checks a rollback's image
/// is still in the registry, and puts ContextKeys.PocAct with the desired state (or, for a delete,
/// ContextKeys.PocRolloutAbsent) into the run. It writes nothing: the GitOps commit writes the row after its commit.
/// </summary>
public sealed class PocActHandler(
    IPocActStore store, PocsConfigSource config, PocActPlan plan, PocRollbackImage images, ILogger<PocActHandler> logger)
    : ICommandHandler<PocActContext>
{
    public async Task<CommandResult> ExecuteAsync(PocActContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (PocActKinds.OfPreset(context.Preset) is not { } act)
            return CommandResult.Fail($"The PocAct step runs in a PoC act preset; '{context.Preset}' is none.");
        if (context.Seeds.PocId is not { } pocId || await store.ReadAsync(pocId, cancellationToken) is not { } poc)
            return CommandResult.Fail($"A {act} run needs the PoC it was seeded with; none was found.");
        var order = new PocActOrder(act, context.Seeds.Revision, context.Seeds.Readiness);
        var target = act == PocActKinds.Rollback && order.Revision is { } n ? await store.RevisionAsync(pocId, n, cancellationToken) : null;
        var pocs = config.Current();
        var decision = plan.Decide(order, poc, target, pocs.ReadinessPathMaxChars);
        if (decision.Refusal is { } refusal) return CommandResult.Fail($"{refusal}.");
        if (decision.Desired is { } wanted && act == PocActKinds.Rollback && await MissingAsync(poc, wanted, target, pocs, cancellationToken) is { } gone)
            return CommandResult.Fail($"{gone}.");
        context.Pipeline.Set(ContextKeys.PocAct, act);
        if (decision.Absent) context.Pipeline.Set(ContextKeys.PocRolloutAbsent, true);
        else context.Pipeline.Set(ContextKeys.PocDesiredState, decision.Desired!);
        logger.LogInformation("PoC {PocId}: {Act} planned", pocId, act);
        return CommandResult.Ok(decision.Absent
            ? $"'{poc.Name}' is to be deleted: its deploy folder is removed and Argo CD prunes it; its source stays."
            : $"'{poc.Name}' ({poc.State}) is to run revision {decision.Desired!.Revision} with {decision.Desired.Replicas} replica(s).");
    }

    private async Task<string?> MissingAsync(
        PocActSubject poc, PocDesiredState wanted, PocActRevision? revision, Contracts.Models.Configuration.PocsConfig pocs, CancellationToken ct)
    {
        if (pocs.Targets.FirstOrDefault(t => string.Equals(t.Name, poc.Target, StringComparison.OrdinalIgnoreCase)) is not { } target)
            return $"'{poc.Name}' names the target '{poc.Target}', which pocs.targets no longer holds";
        try
        {
            return await images.MissingAsync(poc.Name, wanted.Revision, wanted.ImageDigest, revision?.CommitSha, target, ct);
        }
        catch (ContainerRegistryException ex)
        {
            logger.LogWarning(ex, "The rollback image of PoC {PocId} could not be checked", poc.PocId);
            return $"The image of revision {wanted.Revision} of '{poc.Name}' could not be checked: {ex.Message}";
        }
    }
}
