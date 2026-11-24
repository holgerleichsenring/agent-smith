using AgentSmith.Contracts.Providers;
using AgentSmith.Infrastructure.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Providers.Source;

/// <summary>
/// 2026-11-23-c81ea: drops the deletions of paths the branch does not hold, so a retried delete whose commit
/// already landed is no refusal. ONLY the host's not-found counts as absent: the probe answers false for that and
/// throws for anything else, and a throw fails the write naming the path — a transient error must never turn a
/// required deletion into a note.
/// </summary>
public sealed class AbsentDeletionFilter(ILogger logger)
{
    public async Task<DeletionFilterResult> FilterAsync(
        IReadOnlyList<RepoFile> files, Func<string, CancellationToken, Task<bool>> existsAtBase,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(existsAtBase);
        var kept = new List<RepoFile>(files.Count);
        var notes = new List<string>();
        foreach (var file in files)
        {
            if (!file.Delete) { kept.Add(file); continue; }
            var (exists, failure) = await ProbeAsync(file.Path, existsAtBase, cancellationToken);
            if (failure is not null) return new DeletionFilterResult([], [], failure);
            if (exists) kept.Add(file);
            else notes.Add($"`{file.Path}` was already absent, so its deletion was dropped");
        }
        return kept.Count == 0
            ? new DeletionFilterResult([], notes, BranchWriteResult.HeldAlready(notes))
            : new DeletionFilterResult(kept, notes, null);
    }

    private async Task<(bool Exists, BranchWriteResult? Failure)> ProbeAsync(
        string path, Func<string, CancellationToken, Task<bool>> existsAtBase, CancellationToken ct)
    {
        try
        {
            return (await existsAtBase(path, ct), null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Could not tell whether {Path} exists before deleting it", path);
            return (false, BranchWriteResult.Failed(
                $"could not tell whether `{path}` exists to delete it: {ex.Message.Split('\n', 2)[0].Trim()}"));
        }
    }
}
