using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Sandbox.Wire;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services;

/// <summary>
/// p0439: brings a sandbox's source back to the state its last verified phase left, so a
/// shortfall delivers verified work and nothing else.
/// <para>
/// The phase that stopped the run may have left work on the branch — a checkpoint push,
/// the pre-gate commit before a red verification, an edit the master made before it died.
/// None of it was verified, and a ready pull request must not carry it. Every source path
/// changed since the verified head is restored from that head or, when it did not exist
/// there, removed; the run record is not source and stays. The result is proven by asking
/// git the same question again — an answer that is not empty is not a delivery.
/// </para>
/// </summary>
public sealed class UnverifiedWorkReverter(
    SandboxGitOperations gitOps, ILogger<UnverifiedWorkReverter> logger)
{
    private const int GitTimeoutSeconds = 120;

    /// <summary>True when the index now matches the verified head outside the run record.</summary>
    public async Task<bool> RestoreAsync(
        PipelineContext pipeline, string sandboxKey, ISandbox sandbox, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        var head = VerifiedHeads.For(pipeline, sandboxKey);
        if (head is null)
        {
            logger.LogInformation("{Key}: no verified head recorded — nothing can be delivered from it", sandboxKey);
            return false;
        }
        try
        {
            await gitOps.StageAllAsync(sandbox, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex, "{Key}: staging failed — the tree cannot be brought back to {Head}", sandboxKey, head);
            return false;
        }
        var beyond = await BeyondAsync(sandbox, head, cancellationToken);
        if (beyond is null) return false;
        if (beyond.Count == 0) return true;
        if (!await RevertAsync(sandboxKey, sandbox, head, beyond, cancellationToken)) return false;
        var left = await BeyondAsync(sandbox, head, cancellationToken);
        return left is { Count: 0 };
    }

    private async Task<bool> RevertAsync(
        string sandboxKey, ISandbox sandbox, string head,
        IReadOnlyList<(string? Restore, string? Remove)> beyond, CancellationToken ct)
    {
        var restore = beyond.Where(c => c.Restore is not null).Select(c => c.Restore!).Distinct().ToList();
        var remove = beyond.Where(c => c.Remove is not null).Select(c => c.Remove!).Distinct().ToList();
        if (restore.Count > 0 && await RunAsync(sandbox, ["checkout", head, "--", .. restore], ct) is null)
            return false;
        if (remove.Count > 0 && await RunAsync(sandbox, ["rm", "-f", "-q", "--", .. remove], ct) is null)
            return false;
        logger.LogInformation(
            "{Key}: unverified work reverted to {Head} — {Restored} path(s) restored, {Removed} removed",
            sandboxKey, head, restore.Count, remove.Count);
        return true;
    }

    /// <summary>Every source path the index carries beyond the verified head, and what
    /// brings it back: restored from the head, or removed because the head had no such path.</summary>
    private async Task<IReadOnlyList<(string? Restore, string? Remove)>?> BeyondAsync(
        ISandbox sandbox, string head, CancellationToken ct)
    {
        var output = await RunAsync(sandbox, ["diff", "--cached", "--name-status", head], ct);
        if (output is null) return null;
        return [.. output.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(Change)
            .Where(c => c is { } change && (change.Restore is not null || change.Remove is not null))
            .Select(c => c!.Value)];
    }

    // name-status lines: "M\tpath", "A\tpath", "D\tpath", "R100\told\tnew", "C100\told\tnew".
    private static (string? Restore, string? Remove)? Change(string line)
    {
        var parts = line.Split('\t');
        if (parts.Length < 2) return null;
        var kind = parts[0][0];
        var first = parts[1].Trim();
        var last = parts[^1].Trim();
        string? restore = kind is 'A' or 'C' ? null : Source(first);
        string? remove = kind is 'A' or 'C' or 'R' ? Source(last) : null;
        return (restore, remove);
    }

    private static string? Source(string path) => RunRecordPaths.IsRunRecordPath(path) ? null : path;

    private async Task<string?> RunAsync(ISandbox sandbox, IReadOnlyList<string> args, CancellationToken ct)
    {
        var result = await sandbox.RunStepAsync(
            new Step(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Run,
                Command: "git", Args: args, WorkingDirectory: "/work", TimeoutSeconds: GitTimeoutSeconds),
            progress: null, ct);
        if (result.ExitCode == 0) return result.OutputContent ?? string.Empty;
        logger.LogWarning("git {Args} exited {Exit}: {Error}", string.Join(' ', args), result.ExitCode, result.ErrorMessage);
        return null;
    }
}
