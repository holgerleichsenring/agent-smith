using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Sandbox.Wire;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-09-17-042eh: where each sandbox stood when THIS phase was selected.
/// <para>
/// The phase review reads the phase's own diff, and nothing recorded where the phase began.
/// <see cref="DeliveryDiff"/> answers a different question — what the BRANCH delivers against
/// the rung it was cut from — so on the fourth phase of a set it carries the first three
/// phases' work, and a reviewer handed it would report the previous phases' code as this
/// phase's. <see cref="VerifiedHeads"/> is no substitute either: it is written by this phase's
/// own verification, which is the far end of the interval to be measured.
/// </para>
/// <para>
/// Recorded, never merged: the map is REPLACED at every phase, at the same point
/// <see cref="PhaseRepairScope.Reset"/> runs, so a sandbox that has gone away does not leave a
/// stale head behind. A sandbox opened mid-phase has no entry, and the review says so rather
/// than diffing against a commit that has nothing to do with it.
/// </para>
/// </summary>
public sealed class PhaseStartHeads(SandboxTargets targets, ILogger<PhaseStartHeads> logger)
{
    private const int GitTimeoutSeconds = 120;

    /// <summary>Records every resolvable sandbox's HEAD as this phase's starting point.</summary>
    public async Task RecordAsync(PipelineContext pipeline, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        var heads = new Dictionary<string, string>(StringComparer.Ordinal);
        if (targets.TryResolve(pipeline, out var sandboxes, out _))
        {
            foreach (var (key, sandbox) in sandboxes)
                if (await HeadAsync(sandbox, cancellationToken) is { } head) heads[key] = head;
            logger.LogInformation(
                "The phase starts at {Recorded} of {Total} sandbox head(s): {Heads}",
                heads.Count, sandboxes.Count,
                string.Join(", ", heads.Select(h => $"{h.Key}@{Short(h.Value)}")));
        }
        pipeline.Set<IReadOnlyDictionary<string, string>>(ContextKeys.PhaseStartHeads, heads);
    }

    /// <summary>The commit this phase started from in one sandbox, or null when none.</summary>
    public static string? For(PipelineContext pipeline, string sandboxKey)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        return pipeline.TryGet<IReadOnlyDictionary<string, string>>(
            ContextKeys.PhaseStartHeads, out var heads) && heads is not null
            && heads.TryGetValue(sandboxKey, out var head) ? head : null;
    }

    private async Task<string?> HeadAsync(ISandbox sandbox, CancellationToken ct)
    {
        var result = await sandbox.RunStepAsync(
            new Step(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Run,
                Command: "git", Args: ["rev-parse", "HEAD"], WorkingDirectory: "/work",
                TimeoutSeconds: GitTimeoutSeconds),
            progress: null, ct);
        if (result.ExitCode != 0) return null;
        var head = (result.OutputContent ?? string.Empty).Trim();
        return head.Length == 0 ? null : head;
    }

    private static string Short(string sha) => sha.Length <= 8 ? sha : sha[..8];
}
