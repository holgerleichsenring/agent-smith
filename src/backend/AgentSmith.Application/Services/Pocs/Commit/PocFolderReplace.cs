using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Entities;
using AgentSmith.Sandbox.Wire;

namespace AgentSmith.Application.Services.Pocs.Commit;

/// <summary>
/// 2026-11-23-933dc: the folder becomes exactly the given files — its tracked files removed, the files written from
/// memory, the reference marker deleted, and everything staged with 'add -f -A' INSIDE the folder, so a nested
/// .gitignore drops nothing and nothing outside the folder is staged.
/// </summary>
public sealed class PocFolderReplace(PocGit git, ReferenceSetMaterialiser materialiser)
{
    public async Task ReplaceAsync(ISandbox sandbox, string folder, IReadOnlyList<ReferenceSetFile> files, CancellationToken ct)
    {
        await git.RunAsync(sandbox, $"Removing the tracked files of '{folder}'", ["rm", "-r", "-q", "--ignore-unmatch", "--", folder], ct);
        await materialiser.WriteUnderAsync(sandbox, files, folder, ct);
        await git.RunStepAsync(sandbox, "Removing the reference marker", new Step(Step.CurrentSchemaVersion, Guid.NewGuid(),
            StepKind.Run, Command: "rm", Args: ["-f", $"{folder}/{ReferenceSetMaterialiser.Marker}"],
            WorkingDirectory: Repository.SandboxWorkPath), ct);
        await git.RunAsync(sandbox, $"Staging '{folder}'", ["-C", folder, "add", "-f", "-A", "."], ct);
    }
}
