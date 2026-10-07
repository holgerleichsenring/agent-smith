using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Specs;

namespace AgentSmith.Application.Services.PhaseExecution;

/// <summary>
/// 2026-10-06-03c7e: the ONE series commit of a record — the done files, the manifest and the
/// index line staged, the planned files removed — and the pointer re-recorded at its sha with
/// how many leading specs are executed. The pointer moves because the commit is this system's
/// own on the series' paths; left behind, the next run would read it as a reviewer's edit.
/// </summary>
public sealed class SeriesRecordCommit(
    ISandboxFileReaderFactory readerFactory,
    SandboxGitOperations gitOps,
    SpecSetPointerRecorder pointer)
{
    /// <returns>The sha of the series commit.</returns>
    public async Task<string> CommitAsync(
        SeriesRecordTarget target, SpecDoneRecord record, SpecSet executed,
        IReadOnlyList<string> staged, IReadOnlyList<string> planned, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(executed);
        await RemoveAsync(target.Sandbox, planned, ct);
        await gitOps.ForceStagePathsAsync(target.Sandbox, staged, ct);
        await gitOps.CommitAndPushStagedAsync(
            target.Sandbox, target.Branch, MessageFor(executed, record.Phase), record.Carrier, ct);
        var sha = await gitOps.GetHeadCommitAsync(target.Sandbox, ct);
        await pointer.RecordAsync(target.Project, record.Carrier, executed, sha, ct, executed.ExecutedHead.Count);
        return sha;
    }

    /// <summary>The commit message of a record.</summary>
    public static string MessageFor(SpecSet set, SpecPhase phase) =>
        $"spec: {set.Series} ({set.Key}) {phase.PhaseId} executed";

    // An untracked planned file is staged first, so git rm takes it out of the tree as well.
    private async Task RemoveAsync(ISandbox sandbox, IReadOnlyList<string> planned, CancellationToken ct)
    {
        var files = readerFactory.Create(sandbox);
        var present = new List<string>(planned.Count);
        foreach (var path in planned)
            if (await files.ExistsAsync(path, ct)) present.Add(path);
        if (present.Count == 0) return;
        await gitOps.ForceStagePathsAsync(sandbox, present, ct);
        await gitOps.RemoveAsync(sandbox, present, ct);
    }
}
