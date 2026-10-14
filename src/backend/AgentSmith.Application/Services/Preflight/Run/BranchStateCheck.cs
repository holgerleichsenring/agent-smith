using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Preflight;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Sandbox.Wire;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Preflight.Run;

/// <summary>
/// p0428: says out loud whether the ticket branch already carries an earlier run's work.
/// <para>
/// One live run inherited a branch on which source files had been commented out
/// wholesale; specs were treated as delivered, and the agent then read 76 files and
/// wrote nothing for 50 minutes. The fact was visible in ten seconds of git history.
/// </para>
/// <para>
/// A REPORT, never a refusal. p0112 persists work branches and p0360 pushes mid-run
/// checkpoints on purpose, so prior commits are as often the framework resuming itself
/// as they are a poisoned base — failing here would refuse the resume the framework
/// created.
/// </para>
/// <para>
/// 2026-10-13-3890c: only what the branch carries BEYOND its base counts. Read over HEAD's
/// recent history alone, every framework commit another ticket had merged into the base read
/// as this branch's earlier run. The exclusion is ForeignCommitProbe's: merge commits by
/// <c>--no-merges</c>, the base's own history by <c>--not &lt;base&gt;</c>.
/// </para>
/// </summary>
public sealed class BranchStateCheck(SandboxBaseLadder ladder, ILogger<BranchStateCheck> logger) : IRunPreflightCheck
{
    private const int TimeoutSeconds = 60;

    public string Name => "branch-state";

    public async Task<RunPreflightFinding> RunAsync(
        PipelineContext pipeline, CancellationToken cancellationToken)
    {
        if (!pipeline.TryGet<IReadOnlyDictionary<string, ISandbox>>(
                ContextKeys.Sandboxes, out var sandboxes) || sandboxes is null || sandboxes.Count == 0)
            return RunPreflightFinding.Pass(Name, "no sandboxes in this run — no branch to read");

        var carried = new List<string>();
        var parent = RunParentTicket.Of(pipeline);
        foreach (var (key, sandbox) in sandboxes)
        {
            var commits = await CountFrameworkCommitsAsync(sandbox, parent, cancellationToken);
            if (commits > 0) carried.Add($"{key}: {commits}");
        }

        return carried.Count == 0
            ? RunPreflightFinding.Pass(Name, $"{BranchLabel(pipeline)} carries no earlier run's commits")
            : RunPreflightFinding.Warn(
                Name,
                $"{BranchLabel(pipeline)} already carries commits from an earlier run beyond its base "
                + $"({string.Join(", ", carried)}) — that work stays on the branch. The base branch's newer "
                + "commits are merged in on checkout (p0496); deleting the branch would discard the earlier "
                + "run's work and close the pull request hanging off it");
    }

    private static string BranchLabel(PipelineContext pipeline) =>
        pipeline.TryGet<string>(ContextKeys.CheckoutBranch, out var branch)
        && !string.IsNullOrWhiteSpace(branch)
            ? $"branch '{branch}'"
            : "the checked-out branch";

    /// <summary>
    /// Commits authored by the framework identity that the branch carries beyond its base. No
    /// resolvable base means no count: a count over the base's own history is the false report
    /// this replaced.
    /// </summary>
    private async Task<int> CountFrameworkCommitsAsync(ISandbox sandbox, string? parent, CancellationToken ct)
    {
        try
        {
            if ((await ladder.ResolveAsync(sandbox, parent, ct)).Ref is not { } baseRef)
            {
                logger.LogDebug("No base ref in sandbox {JobId} — this sandbox contributes no branch report", sandbox.JobId);
                return 0;
            }
            var step = new Step(
                Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Run,
                Command: "git",
                Args: ["rev-list", "--no-merges", "--format=%ae", "HEAD", "--not", baseRef],
                WorkingDirectory: Repository.SandboxWorkPath,
                TimeoutSeconds: TimeoutSeconds);
            var result = await sandbox.RunStepAsync(step, null, ct);
            if (result.ExitCode != 0) return 0;
            return CountAuthored(result.OutputContent ?? string.Empty);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogDebug(ex, "Branch history read failed — this sandbox contributes no branch report");
            return 0;
        }
    }

    internal static int CountAuthored(string log) =>
        log.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Count(line => line.Trim().Equals(SandboxGitIdentity.Email, StringComparison.OrdinalIgnoreCase));
}
