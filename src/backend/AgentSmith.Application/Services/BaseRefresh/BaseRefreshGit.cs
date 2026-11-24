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
        // 2026-11-24-9dc0a: names unquoted, so a non-ASCII path reads as the file it is.
        return sandbox.RunStepAsync(new Step(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Run, Command: "git",
            Args: ["-c", "core.quotePath=false", .. args],
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

    /// <summary>2026-11-24-9dc0a: the conflict marker lines each of <paramref name="paths"/> still carries on disk (paths
    /// without one are absent); null when the check itself fails, so a tree nobody could read is never called whole.</summary>
    public async Task<IReadOnlyDictionary<string, int>?> MarkedAsync(ISandbox sandbox, IReadOnlyList<string> paths, CancellationToken ct)
    {
        const string script = "for f in \"$@\"; do [ -f \"$f\" ] && n=$(grep -cE '^(<<<<<<<|>>>>>>>)( |$)' \"$f\") "
            + "&& [ \"$n\" -gt 0 ] && echo \"$n $f\"; done; true";
        if (await ShellLinesAsync(sandbox, script, paths, ct) is not { } lines) return null;
        return lines.Select(l => l.Split(' ', 2)).Where(p => p.Length == 2 && int.TryParse(p[0], out _))
            .ToDictionary(p => p[1], p => int.Parse(p[0], System.Globalization.CultureInfo.InvariantCulture), StringComparer.Ordinal);
    }

    /// <summary>2026-11-24-9dc0a: which of <paramref name="paths"/> are gone from disk; null when the check fails.</summary>
    public Task<IReadOnlyList<string>?> MissingAsync(ISandbox sandbox, IReadOnlyList<string> paths, CancellationToken ct) =>
        ShellLinesAsync(sandbox, "for f in \"$@\"; do [ -e \"$f\" ] || echo \"$f\"; done; true", paths, ct);

    /// <summary>2026-11-24-9dc0a: every line git printed, trimmed; null when it failed.</summary>
    public async Task<IReadOnlyList<string>?> LinesAsync(ISandbox sandbox, IReadOnlyList<string> args, CancellationToken ct)
    {
        var result = await RunAsync(sandbox, args, ct);
        return result.ExitCode != 0 ? null : Split(result.OutputContent);
    }

    private static async Task<IReadOnlyList<string>?> ShellLinesAsync(ISandbox sandbox, string script, IReadOnlyList<string> paths, CancellationToken ct)
    {
        if (paths.Count == 0) return [];
        var found = await sandbox.RunStepAsync(new Step(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Run,
            Command: "sh", Args: ["-c", script, "sh", .. paths], WorkingDirectory: Repository.SandboxWorkPath,
            TimeoutSeconds: WorkBranchBaseMerger.TimeoutSeconds), progress: null, ct);
        return found.ExitCode != 0 ? null : Split(found.OutputContent);
    }

    private static IReadOnlyList<string> Split(string? output) =>
        [.. (output ?? string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];

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
        return await LinesAsync(sandbox, ["diff", "--name-only", "--diff-filter=A", $"{fork}..{other}", "--", ".agentsmith/specs"], ct) ?? [];
    }
}
