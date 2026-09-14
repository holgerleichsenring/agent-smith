using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// 2026-09-13-35a4: makes sure a feature's branch EXISTS in this repository before a slice
/// is cut from it — created by whichever slice reaches the repository first, adopted by
/// every other one.
/// <para>
/// PUBLISHED BEFORE THE CUT, because the ladder must not lie. 2026-09-13-5cdf resolves a
/// rung by asking whether it exists; if creation happened after the cut, the first slice
/// would resolve a fall-through, cut from the clone's own base, and then publish a rung it
/// is not on.
/// </para>
/// <para>
/// LOSING THE RACE IS THE NORMAL OUTCOME for the second slice, so it is answered with the
/// winner's branch rather than an error — see <see cref="RemoteBranchCreation"/>. The
/// adopt path is also the create path's tail — and the FAILED path's tail: a fetch, then
/// the ladder asked again. The remote decides who owns the rung, not this clone's push
/// result, so a create, a refusal and a push that never ran all end in the same question,
/// which is whether the branch is there now. That costs one git call on the run that
/// created the rung and keeps the answer a fact about the clone rather than an assumption
/// about what push left behind.
/// </para>
/// <para>
/// A repository no slice touches never grows a branch: without a parent stamp there is no
/// rung to compose, and nothing is pushed.
/// </para>
/// </summary>
public sealed class SandboxRungPublisher(
    SandboxBaseLadder ladder,
    CreateOnlyBranchPush push,
    ILogger<SandboxRungPublisher> logger)
{
    /// <summary>
    /// The base this repository's slice belongs on, with the rung published if it was this
    /// run's job to publish it. Never throws for a branch it could not create — a rung that
    /// cannot be published leaves the run exactly where it was before rungs existed.
    /// </summary>
    public async Task<ResolvedBase> EnsureAsync(
        ISandbox sandbox, RepoConnection config, string? parentTicketId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(sandbox);
        ArgumentNullException.ThrowIfNull(config);

        var resolved = await ladder.ResolveAsync(sandbox, parentTicketId, ct);
        if (!resolved.FellThrough) return resolved;
        if (string.IsNullOrWhiteSpace(parentTicketId)) return resolved;
        if (resolved.Ref is not { } baseRef)
        {
            logger.LogInformation(
                "This clone names no base, so there is no sha to publish a feature branch at");
            return resolved;
        }

        var rung = TicketBranchNamer.Compose(new TicketId(parentTicketId.Trim())).Value;
        var creation = await push.CreateAsync(sandbox, config, baseRef, rung, ct);
        if (creation == RemoteBranchCreation.Failed)
            logger.LogWarning(
                "Publishing '{Rung}' at {BaseRef} did not succeed — if a sibling slice "
                + "published it anyway, this run still belongs on it", rung, baseRef);
        // The REMOTE decides who owns the rung, not this clone's push result: a create,
        // a refusal and a push that never ran all end in the same question, which is
        // whether the branch is there now.
        return await AdoptAsync(sandbox, config, parentTicketId, rung, ct);
    }

    private async Task<ResolvedBase> AdoptAsync(
        ISandbox sandbox, RepoConnection config, string parentTicketId, string rung, CancellationToken ct)
    {
        await sandbox.RunStepAsync(
            CheckoutStepFactory.BuildFetchRevisionStep(config, rung), progress: null, ct);

        var after = await ladder.ResolveAsync(sandbox, parentTicketId, ct);
        if (!after.FellThrough) return after;

        logger.LogWarning(
            "'{Rung}' is not readable in this clone after the publish, so this slice is cut "
            + "from the clone's own base — which is what every run did before feature "
            + "branches existed", rung);
        return after;
    }
}
