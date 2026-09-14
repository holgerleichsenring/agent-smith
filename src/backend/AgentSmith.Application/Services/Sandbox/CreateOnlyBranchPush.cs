using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Sandbox.Wire;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// 2026-09-13-35a4: publishes a ref that does not exist yet — no force, no lease.
/// <para>
/// DELIBERATELY NOT <c>GitBranchPusher</c>. That one pushes <c>--force-with-lease</c> and,
/// when the remote answers "stale info", FETCHES and pushes again — the fetch makes the
/// lease satisfiable, so the second attempt succeeds by overwriting. That is a correct
/// recovery for a work branch ONE run owns. Pointed at a feature rung several slices merge
/// into, two siblings starting together would race and the loser would force the rung back
/// to the base, deleting whatever had already been merged into it. A shared branch gets its
/// own primitive and never touches that one.
/// </para>
/// <para>
/// A plain push is also the only form that can be REFUSED, and the refusal is the signal
/// this phase needs: the remote, not this clone, decides who created the branch.
/// </para>
/// </summary>
public sealed class CreateOnlyBranchPush(ILogger<CreateOnlyBranchPush> logger)
{
    /// <summary>Pushes <paramref name="atRef"/>'s sha to <c>refs/heads/{branch}</c> on origin.</summary>
    public async Task<RemoteBranchCreation> CreateAsync(
        ISandbox sandbox, RepoConnection config, string atRef, string branch, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(sandbox);
        var result = await sandbox.RunStepAsync(
            CheckoutStepFactory.BuildCreateRemoteBranchStep(config, atRef, branch), progress: null, ct);

        if (result.ExitCode == 0)
        {
            logger.LogInformation("Published '{Branch}' at {AtRef} in sandbox {JobId}",
                branch, atRef, sandbox.JobId);
            return RemoteBranchCreation.Created;
        }

        if (RefusedAsExisting(result))
        {
            logger.LogInformation(
                "'{Branch}' was created by somebody else while this run was composing it — "
                + "adopting theirs", branch);
            return RemoteBranchCreation.AlreadyThere;
        }

        logger.LogWarning("Publishing '{Branch}' failed (exit={Exit}): {Error}",
            branch, result.ExitCode, result.ErrorMessage);
        return RemoteBranchCreation.Failed;
    }

    // git refuses a non-fast-forward create with "[rejected]" plus one of these reasons.
    // The wording is matched rather than the exit code because every push failure exits 1,
    // and a refusal that is actually an authentication problem must not be read as a race.
    private static bool RefusedAsExisting(StepResult result)
    {
        string[] refusals = ["non-fast-forward", "fetch first", "already exists", "stale info"];
        var said = (result.ErrorMessage ?? string.Empty) + '\n' + (result.OutputContent ?? string.Empty);
        return said.Contains("[rejected]", StringComparison.OrdinalIgnoreCase)
               && refusals.Any(r => said.Contains(r, StringComparison.OrdinalIgnoreCase));
    }
}
