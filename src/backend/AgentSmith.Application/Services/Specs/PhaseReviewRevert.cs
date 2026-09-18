using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Models;
using AgentSmith.Sandbox.Wire;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-09-17-042eh: undoes a fix pass that did not come back green, so the phase and the run
/// stand exactly where the FIRST verification left them.
/// <para>
/// The review may not fail a phase that verified. A fix pass is an attempt at an improvement
/// on top of delivered work, and when it fails the right outcome is the work that was already
/// proven — not a failed phase and not a branch carrying unverified code. So every sandbox is
/// brought back to its verified head, the restored state is committed FORWARD (no history is
/// rewritten: the fix and its undo both show in the open draft pull request, which is what a
/// person reads), the account snapshot is put back, and the findings stand marked reverted.
/// </para>
/// <para>
/// A sandbox that cannot be restored OR COMMITTED fails the phase. The fix pass has already
/// committed and pushed its red work, and the push primitive reports nothing when it declines
/// — a secret-scan match, a repo with no matching sandbox, a swallowed exception — so the
/// outcome is PROVEN afterwards: git is asked whether the committed tree still differs from
/// the verified head outside the run record.
/// </para>
/// </summary>
public sealed class PhaseReviewRevert(
    SandboxTargets targets,
    UnverifiedWorkReverter reverter,
    RunWorkCheckpointer checkpointer,
    ILogger<PhaseReviewRevert> logger)
{
    private const int GitTimeoutSeconds = 120;

    /// <summary>
    /// Null when this verdict is not a fix pass's — every other caller records it unchanged.
    /// </summary>
    public async Task<CommandResult?> TryRevertAsync(
        PipelineContext pipeline, CommandResult verdict, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(verdict);
        if (verdict.IsSuccess || !PhaseReviewFixPass.InFlight(pipeline)) return null;

        var reason = verdict.Message.Split('\n', 2)[0].Trim();
        logger.LogWarning(
            "The review's fix pass is not green ({Reason}) — reverting to the verified heads", reason);
        if (await UndoAsync(pipeline, cancellationToken) is { } failure)
            return CommandResult.Fail(
                $"The review's fix pass could not be reverted: {failure}. The tree carries "
                + $"unverified work and the phase is not done. The fix pass failed with: {reason}");

        PhaseReviewFixPass.RestoreSnapshot(pipeline);
        var note = $"fix pass reverted: {reason}";
        pipeline.Set(ContextKeys.PhaseReviewReverted, note);
        var report = PhaseReviewLedger.ForThisPhase(pipeline);
        PhaseReviewLedger.Record(
            pipeline, report with { Findings = [.. report.Findings.Select(f => f with { Reverted = note })] });
        return CommandResult.Ok(
            $"The review's fix pass was not green and has been reverted to the verified state "
            + $"({reason}). The phase stands as its first verification left it; "
            + $"{report.Findings.Count} finding(s) remain.");
    }

    /// <summary>Why the undo failed, or null when every sandbox is back at its verified head
    /// AND that state is on a commit.</summary>
    private async Task<string?> UndoAsync(PipelineContext pipeline, CancellationToken ct)
    {
        // Not a success: a fix pass ran in SOME tree. Reporting "reverted" over a sandbox map
        // that no longer resolves would record the phase done over work nobody brought back.
        if (!targets.TryResolve(pipeline, out var sandboxes, out _))
            return "no sandbox could be resolved, so nothing could be brought back";
        foreach (var (key, sandbox) in sandboxes)
            if (!await reverter.RestoreAsync(pipeline, key, sandbox, ct))
                return $"{key} could not be brought back to its verified head";

        // RepoWorkPusher commits nothing when nothing is staged, which is the ordinary case for
        // a sandbox the fix never touched — so this is the primitive, not the bare commit. It
        // also reports nothing when it declines, which is why the result is asked for below.
        await checkpointer.PushNowAsync(pipeline, ct);
        foreach (var (key, sandbox) in sandboxes)
            if (await UncommittedBeyondAsync(pipeline, key, sandbox, ct) is { } beyond)
                return beyond;
        return null;
    }

    /// <summary>What this sandbox's HEAD still carries beyond the verified head, outside the
    /// run record — null when the restored state IS on a commit. A tree brought back but never
    /// committed leaves the red fix on the branch, which is what a person would merge.</summary>
    private async Task<string?> UncommittedBeyondAsync(
        PipelineContext pipeline, string key, ISandbox sandbox, CancellationToken ct)
    {
        if (VerifiedHeads.For(pipeline, key) is not { } head)
            return $"{key} has no verified head to prove the revert against";
        var output = await RunAsync(sandbox, ["diff", "--name-only", head, "HEAD"], ct);
        if (output is null) return $"{key} could not be compared against its verified head";
        var source = output.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(path => path.Trim())
            .Where(path => !RunRecordPaths.IsRunRecordPath(path))
            .ToList();
        return source.Count == 0
            ? null
            : $"{key} still carries {source.Count} unverified source path(s) on its branch "
              + $"({string.Join(", ", source.Take(3))}) — the restored state was not committed";
    }

    private async Task<string?> RunAsync(ISandbox sandbox, string[] args, CancellationToken ct)
    {
        var result = await sandbox.RunStepAsync(
            new Step(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Run, Command: "git",
                Args: args, WorkingDirectory: "/work", TimeoutSeconds: GitTimeoutSeconds),
            progress: null, ct);
        if (result.ExitCode == 0) return result.OutputContent ?? string.Empty;
        logger.LogWarning("git {Args} exited {Exit}", string.Join(' ', args), result.ExitCode);
        return null;
    }
}
