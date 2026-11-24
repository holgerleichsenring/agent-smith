using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Pocs;
using AgentSmith.Application.Services.Pocs.Rollout;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Exceptions;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Pocs;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// 2026-11-23-c81ec: the last step of a PoC run — follows the rollout the GitOps commit started on the target's Argo CD
/// (<see cref="PocRolloutWatch"/>) until the live Deployment carries this render and Argo reports it Synced and Healthy,
/// or it fails, or its budget (<see cref="PocRolloutDeadline"/>) ends; then <see cref="PocRolloutRecord"/> writes the
/// revision's rollout state and the PoC state. A refusal by Argo CD (token, permission) fails the run and records
/// nothing: whether the pods serve is then unknown, and a retry reads again.
/// </summary>
public sealed class PocRolloutFollowHandler(
    IPocGitOpsStore pocs, IPocRolloutStore store, PocsConfigSource config, IArgoCdClientFactory argos, PocRolloutExpectation expectation,
    PocRolloutDeadline deadlines, PocRolloutWatch watch, PocRolloutRecord record, ILogger<PocRolloutFollowHandler> logger)
    : ICommandHandler<PocRolloutFollowContext>
{
    public async Task<CommandResult> ExecuteAsync(PocRolloutFollowContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.Seeds.PocId is not { } pocId || await pocs.ReadAsync(pocId, cancellationToken) is not { } poc)
            return CommandResult.Fail("A PoC run's rollout follow needs the PoC it was seeded with; none was found.");
        var all = config.Current();
        if (all.Targets.FirstOrDefault(t => string.Equals(t.Name, poc.Target, StringComparison.OrdinalIgnoreCase)) is not { } target)
            return CommandResult.Fail($"'{poc.Name}' names the target '{poc.Target}', which pocs.targets no longer holds.");
        if (target.Argo is not { } argo)
            return CommandResult.Fail($"{PocArgoConfig.Key} of target '{target.Name}' is missing; the rollout of '{poc.Name}' cannot be followed.");
        var (expected, refused) = expectation.Of(context.Pipeline, poc, target);
        if (expected is null) return CommandResult.Fail($"The rollout of '{poc.Name}' cannot be followed: {refused}.");
        if (!expected.Absent && context.Pipeline.TryGet<string>(ContextKeys.PocCommitSha, out var sha) && !string.IsNullOrEmpty(sha))
            await store.RecordCommitAsync(poc.PocId, poc.Revision!.Value, sha, cancellationToken);
        var budget = await deadlines.OfAsync(context.Pipeline, all, cancellationToken);
        return await FollowAsync(context, poc, target, argo, expected, budget, all.Rollout.PollSeconds, cancellationToken);
    }

    private async Task<CommandResult> FollowAsync(
        PocRolloutFollowContext context, PocGitOpsSubject poc, PocTargetConfig target, PocArgoConfig argo, PocRolloutExpected expected,
        PocRolloutBudget budget, int pollSeconds, CancellationToken ct)
    {
        try
        {
            var (reading, _) = await watch.WatchAsync(context.Pipeline, argos.Create(argo), expected, budget, pollSeconds, ct);
            var host = target.DomainPattern?.Replace("{app}", poc.Name, StringComparison.Ordinal);
            var act = context.Pipeline.TryGet<string>(ContextKeys.PocAct, out var carried) ? carried : null; // 2026-11-23-c81ed
            return await record.EndAsync(new PocRolloutEnd(poc.PocId, poc.Name, poc.Revision ?? 0, host, expected, reading, budget, act), ct);
        }
        catch (ArgoCdException ex)
        {
            logger.LogWarning(ex, "The rollout follow of PoC {PocId} was refused by Argo CD", poc.PocId);
            return CommandResult.Fail($"The rollout of '{poc.Name}' could not be followed: {ex.Message}. Nothing was recorded; a retry reads again.");
        }
    }
}
