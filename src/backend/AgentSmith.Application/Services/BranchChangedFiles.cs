using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Sandbox.Wire;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services;

/// <summary>
/// 2026-10-13-3890b: the files one sandbox's branch carries — committed in any pass of the run,
/// uncommitted, or never added — named by git rather than remembered by a tool. The comparison
/// walks the delivery diff's ladder (base, else run start, else HEAD); untracked files come from
/// <c>git ls-files --others --exclude-standard</c>. Paths are repository-relative.
/// </summary>
public sealed class BranchChangedFiles(
    SandboxBaseLadder baseLadder,
    SandboxRunStartCommit runStartReader,
    ILogger<BranchChangedFiles> logger)
{
    private const int GitTimeoutSeconds = 120;

    /// <summary>The branch's files, or null when no rung of the ladder could be read.</summary>
    public async Task<IReadOnlyList<CodeChange>?> ReadAsync(
        ISandbox sandbox, DeliveryBasis basis, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sandbox);
        ArgumentNullException.ThrowIfNull(basis);
        var resolved = await baseLadder.ResolveAsync(sandbox, basis.ParentTicketId, cancellationToken);
        var runStart = await runStartReader.ResolveAsync(sandbox, basis.RunId, cancellationToken);
        foreach (var (against, description) in DeliveryDiffCandidates.Ordered(resolved.Name, runStart))
        {
            var nameStatus = await GitAsync(sandbox, ["diff", "--name-status", .. against], cancellationToken);
            if (nameStatus is null) continue;
            var untracked = await GitAsync(
                sandbox, ["ls-files", "--others", "--exclude-standard"], cancellationToken);
            if (untracked is null) return null;
            var files = ParseNameStatus(nameStatus).Concat(ParseUntracked(untracked)).ToList();
            logger.LogInformation("Branch files taken {Description}: {Count}", description, files.Count);
            return files;
        }
        return null;
    }

    private static async Task<string?> GitAsync(
        ISandbox sandbox, string[] args, CancellationToken cancellationToken)
    {
        var result = await sandbox.RunStepAsync(
            new Step(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Run,
                Command: "git", Args: args, WorkingDirectory: Repository.SandboxWorkPath,
                TimeoutSeconds: GitTimeoutSeconds),
            progress: null, cancellationToken);
        return result.ExitCode == 0 ? result.OutputContent ?? string.Empty : null;
    }

    /// <summary>One line per path: a status letter (R and C carry a score and two paths, the
    /// second being the file the branch now has), a tab, the path.</summary>
    private static IEnumerable<CodeChange> ParseNameStatus(string output) =>
        Lines(output)
            .Select(line => line.Split('\t'))
            .Where(parts => parts.Length >= 2)
            .Select(parts => Change(parts[^1], ChangeTypeOf(parts[0])));

    private static IEnumerable<CodeChange> ParseUntracked(string output) =>
        Lines(output).Select(path => Change(path, "Create"));

    private static string ChangeTypeOf(string status) => status.FirstOrDefault() switch
    {
        'A' => "Create",
        'D' => "Delete",
        _ => "Modify",
    };

    private static CodeChange Change(string path, string changeType) =>
        new(new FilePath(path.Trim()), string.Empty, changeType);

    private static IEnumerable<string> Lines(string output) =>
        output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
