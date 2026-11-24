using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Sandbox.Wire;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// 2026-09-13-5cdf: resolves ONE base per repository — the parent's rung when this clone
/// has it, otherwise the clone's own base.
/// <para>
/// A LADDER, because it degrades. A rung that does not exist costs a ref check and the
/// next candidate is tried; an estate that has no rung at all lands on the clone's base,
/// which is what every run did before. That makes the change backward-compatible by
/// construction rather than by a flag.
/// </para>
/// <para>
/// No provider API is involved: the sandbox clone is FULL (no <c>--depth</c>, no
/// <c>--single-branch</c>), so every remote branch is already reachable and a rung is
/// found with a ref check. Nothing here creates or publishes a branch: 2026-09-13-35a4's
/// <see cref="SandboxRungPublisher"/> does, and asks this ladder both before and after its
/// push, so what a slice cuts from stays a fact read off the clone.
/// </para>
/// <para>
/// TWO rungs, not three: the caller names one parent per repository — an epic child's label parent
/// (<c>FiledTicketLabels.ParentId</c>), or 2026-11-23-31dca a stacked run's entry for THIS repository, chosen at the
/// claim from the chain of unlanded prerequisites with work there. The ladder walks no chain: the claim did. A
/// stacked rung that is missing here is the caller's stop, not a fall-through (<see cref="SandboxWorkBranchCheckout"/>).
/// </para>
/// </summary>
public sealed class SandboxBaseLadder(
    SandboxBaseBranch cloneBase, ILogger<SandboxBaseLadder> logger)
{
    private const int TimeoutSeconds = 120;

    /// <param name="parentTicketId">
    /// The epic record this ticket is a slice of, or null when it is not a slice — in
    /// which case there is no rung to look for and the clone's base is the answer.
    /// </param>
    public async Task<ResolvedBase> ResolveAsync(
        ISandbox sandbox, string? parentTicketId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sandbox);
        foreach (var rung in Candidates(parentTicketId))
        {
            if (await ExistsAsync(sandbox, rung, cancellationToken))
            {
                logger.LogInformation(
                    "Base resolved to the feature rung '{Rung}' in sandbox {JobId}",
                    rung, sandbox.JobId);
                return ResolvedBase.Rung(rung);
            }
            logger.LogInformation(
                "'{Rung}' does not exist in sandbox {JobId} — the ladder falls through to "
                + "this clone's own base", rung, sandbox.JobId);
        }

        return ResolvedBase.CloneBase(await cloneBase.ResolveAsync(sandbox, cancellationToken));
    }

    /// <summary>
    /// Ordered rung candidates, closest ancestor first — the shape
    /// <c>DeliveryDiff</c> already uses for its comparison ladder. The rung carries the
    /// framework's own branch convention, so the name a slice looks for is the name the
    /// publisher will compose.
    /// </summary>
    private static IEnumerable<string> Candidates(string? parentTicketId)
    {
        if (string.IsNullOrWhiteSpace(parentTicketId)) yield break;
        yield return TicketBranchNamer.RungOf(parentTicketId).Value; // 2026-11-24-f9c5b: a series branch is its own name
    }

    private async Task<bool> ExistsAsync(
        ISandbox sandbox, string rung, CancellationToken cancellationToken)
    {
        var result = await sandbox.RunStepAsync(
            new Step(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Run,
                Command: "git",
                Args: ["rev-parse", "--verify", "--quiet", $"refs/remotes/origin/{rung}"],
                WorkingDirectory: Repository.SandboxWorkPath, TimeoutSeconds: TimeoutSeconds),
            progress: null, cancellationToken);
        return result.ExitCode == 0;
    }
}
