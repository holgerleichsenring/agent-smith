using System.Text.RegularExpressions;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Sandbox.Wire;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// 2026-11-17-023fb: resolves a base merge whose ONLY conflicts are context.yaml files where both sides added
/// state.done index lines — the collision every pair of parallel specs produces. Each file's three stages are read
/// from git's index, united by <see cref="IContextYamlStateIndexUnion"/>, written and staged. Anything else, or a file
/// the union refuses, answers false and leaves the merge for the caller to abort.
/// </summary>
public sealed partial class StateIndexConflictResolver(
    IContextYamlStateIndexUnion union, ILogger<StateIndexConflictResolver> logger)
{
    /// <summary>True when every conflicting path was resolved and staged.</summary>
    public async Task<bool> TryResolveAsync(ISandbox sandbox, IReadOnlyList<string> conflicting, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(conflicting);
        if (conflicting.Count == 0 || !conflicting.All(p => ContextYaml().IsMatch(p))) return false;
        foreach (var path in conflicting)
        {
            var resolved = union.Resolve(
                await StageAsync(sandbox, 1, path, ct), await StageAsync(sandbox, 2, path, ct), await StageAsync(sandbox, 3, path, ct));
            if (resolved is null)
            {
                logger.LogInformation("{Path}: the conflict is more than added state.done entries", path);
                return false;
            }
            var write = await sandbox.RunStepAsync(new Step(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.WriteFile,
                TimeoutSeconds: WorkBranchBaseMerger.TimeoutSeconds, Path: path, Content: resolved), progress: null, ct);
            if (write.ExitCode != 0 || (await GitAsync(sandbox, ["add", "--", path], ct)).ExitCode != 0) return false;
            logger.LogInformation("{Path}: state.done entries of both sides united", path);
        }
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
