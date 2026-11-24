using System.Text.RegularExpressions;
using AgentSmith.Application.Services.Memory;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Sandbox.Wire;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// 2026-11-17-023fb: resolves the base-merge conflicts of the append-only indexes every pair of parallel specs collides
/// in — context.yaml files where both sides added state.done index lines (<see cref="IContextYamlStateIndexUnion"/>) and,
/// 2026-11-24-6811b, the memory index MEMORY.md (<see cref="MemoryIndexUnion"/>). Each file's three stages are read from
/// git's index, united, written and staged. Every other path, and an index file a union refuses, is handed back unmerged
/// for the caller to resolve or abort.
/// </summary>
public sealed partial class StateIndexConflictResolver(
    IContextYamlStateIndexUnion union, ILogger<StateIndexConflictResolver> logger)
{
    /// <summary>The memory index a union resolves (ProjectMetaPaths.Memory + MEMORY.md).</summary>
    public const string MemoryIndexPath = ProjectMetaPaths.Memory + "/" + ProjectMetaPaths.MemoryIndexFile;

    /// <summary>Resolves and stages what it can; answers the conflicting paths still unmerged.</summary>
    public async Task<IReadOnlyList<string>> ResolveAsync(ISandbox sandbox, IReadOnlyList<string> conflicting, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(conflicting);
        var unresolved = new List<string>();
        foreach (var path in conflicting)
            if (!await TryResolveAsync(sandbox, path, ct)) unresolved.Add(path);
        return unresolved;
    }

    private async Task<bool> TryResolveAsync(ISandbox sandbox, string path, CancellationToken ct)
    {
        Func<string?, string?, string?, string?>? unite = ContextYaml().IsMatch(path) ? union.Resolve
            : path == MemoryIndexPath ? MemoryIndexUnion.Resolve : null;
        if (unite is null) return false;
        var resolved = unite(await StageAsync(sandbox, 1, path, ct), await StageAsync(sandbox, 2, path, ct), await StageAsync(sandbox, 3, path, ct));
        if (resolved is null)
        {
            logger.LogInformation("{Path}: the conflict is more than index lines both sides added", path);
            return false;
        }
        var write = await sandbox.RunStepAsync(new Step(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.WriteFile,
            TimeoutSeconds: WorkBranchBaseMerger.TimeoutSeconds, Path: path, Content: resolved), progress: null, ct);
        if (write.ExitCode != 0 || (await GitAsync(sandbox, ["add", "--", path], ct)).ExitCode != 0) return false;
        logger.LogInformation("{Path}: index lines of both sides united", path);
        return true;
    }

    // git show :N:path — 1 is the merge base, 2 ours, 3 theirs; a stage that does not exist answers null.
    private static async Task<string?> StageAsync(ISandbox sandbox, int stage, string path, CancellationToken ct)
    {
        var result = await GitAsync(sandbox, ["show", $":{stage}:{path}"], ct);
        return result.ExitCode == 0 ? result.OutputContent ?? string.Empty : null;
    }

    private static Task<StepResult> GitAsync(ISandbox sandbox, IReadOnlyList<string> args, CancellationToken ct) =>
        sandbox.RunStepAsync(new Step(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Run, Command: "git", Args: args,
            WorkingDirectory: Repository.SandboxWorkPath, TimeoutSeconds: WorkBranchBaseMerger.TimeoutSeconds), progress: null, ct);

    [GeneratedRegex(@"^\.agentsmith/contexts/[^/]+/context\.yaml$")]
    private static partial Regex ContextYaml();
}
