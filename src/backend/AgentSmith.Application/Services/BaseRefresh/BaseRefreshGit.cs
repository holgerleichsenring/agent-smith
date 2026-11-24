using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Entities;
using AgentSmith.Sandbox.Wire;

namespace AgentSmith.Application.Services.BaseRefresh;

/// <summary>2026-11-24-6811c: the git calls a refresh-base run makes around its merge, in the repository's sandbox, and
/// the conflict-marker check a resolved merge must pass.</summary>
public sealed class BaseRefreshGit
{
    public Task<StepResult> RunAsync(ISandbox sandbox, IReadOnlyList<string> args, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(sandbox);
        return sandbox.RunStepAsync(new Step(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Run, Command: "git", Args: args,
            WorkingDirectory: Repository.SandboxWorkPath, TimeoutSeconds: WorkBranchBaseMerger.TimeoutSeconds), progress: null, ct);
    }

    /// <summary>The first line git printed, trimmed; null when it failed or printed nothing.</summary>
    public async Task<string?> LineAsync(ISandbox sandbox, IReadOnlyList<string> args, CancellationToken ct, bool tolerateOne = false)
    {
        var result = await RunAsync(sandbox, args, ct);
        if (result.ExitCode != 0 && !(tolerateOne && result.ExitCode == 1)) return null;
        var line = (result.OutputContent ?? string.Empty).Split('\n', 2)[0].Trim();
        return line.Length == 0 ? null : line;
    }

    /// <summary>Stages each path as it now is — added or changed when it exists, removed from the index when it was deleted
    /// (a modify/delete conflict resolved by removal); a path neither on disk nor in the index is passed over.</summary>
    public async Task<bool> StageAsync(ISandbox sandbox, IReadOnlyList<string> paths, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(sandbox);
        const string script = "for f in \"$@\"; do if [ -e \"$f\" ]; then git add -A -- \"$f\" || exit 1; "
            + "else git rm -q --cached --ignore-unmatch -- \"$f\" || exit 1; fi; done";
        var staged = await sandbox.RunStepAsync(new Step(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Run,
            Command: "sh", Args: ["-c", script, "sh", .. paths], WorkingDirectory: Repository.SandboxWorkPath,
            TimeoutSeconds: WorkBranchBaseMerger.TimeoutSeconds), progress: null, ct);
        return staged.ExitCode == 0;
    }

    /// <summary>The working tree at HEAD exactly: tracked files reset, untracked ones removed (ignored ones kept), so a
    /// stage builds what a push would carry.</summary>
    public async Task LevelAsync(ISandbox sandbox, string commit, CancellationToken ct)
    {
        await RunAsync(sandbox, ["reset", "--hard", commit], ct);
        await RunAsync(sandbox, ["clean", "-fdq"], ct);
    }

    /// <summary>2026-11-24-6811c: whether any of <paramref name="paths"/> still carries a conflict marker line; true also
    /// when the check itself fails, so a tree nobody could read is never called whole.</summary>
    public async Task<bool> HasMarkersAsync(ISandbox sandbox, IReadOnlyList<string> paths, CancellationToken ct)
    {
        const string script = "for f in \"$@\"; do [ -f \"$f\" ] && grep -qE '^(<<<<<<<|>>>>>>>)( |$)' \"$f\" && echo \"$f\"; done; true";
        var found = await sandbox.RunStepAsync(new Step(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Run,
            Command: "sh", Args: ["-c", script, "sh", .. paths], WorkingDirectory: Repository.SandboxWorkPath,
            TimeoutSeconds: WorkBranchBaseMerger.TimeoutSeconds), progress: null, ct);
        return found.ExitCode != 0 || !string.IsNullOrWhiteSpace(found.OutputContent);
    }

    /// <summary>Puts the branch back where it was before the merge: no merge in progress, tracked files at that head.</summary>
    public async Task ResetAsync(ISandbox sandbox, string head, CancellationToken ct)
    {
        await RunAsync(sandbox, ["merge", "--abort"], ct);
        await RunAsync(sandbox, ["reset", "--hard", head], ct);
    }

    /// <summary>The spec files added between the merge base of <paramref name="head"/> and <paramref name="other"/> and
    /// <paramref name="other"/> — what one side of the merge set out to do.</summary>
    public async Task<IReadOnlyList<string>> SpecsAddedAsync(ISandbox sandbox, string head, string other, CancellationToken ct)
    {
        if (await LineAsync(sandbox, ["merge-base", head, other], ct) is not { } fork) return [];
        var result = await RunAsync(sandbox, ["diff", "--name-only", "--diff-filter=A", $"{fork}..{other}", "--", ".agentsmith/specs"], ct);
        return result.ExitCode != 0 ? [] : [.. (result.OutputContent ?? string.Empty)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];
    }
}
