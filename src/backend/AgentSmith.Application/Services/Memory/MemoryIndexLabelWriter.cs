using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;

namespace AgentSmith.Application.Services.Memory;

/// <summary>
/// 2026-10-25-9324d: the record step's rewrite of the carrier repository's MEMORY.md labels from the evidence —
/// the one writer of a label, so the file in git says what the runs decided. Answers the repository path when
/// the file changed, for the record's staged list; null when there is no index or nothing moved.
/// </summary>
public sealed class MemoryIndexLabelWriter(MemoryStandingReader standings, MemoryIndexLabels labels)
{
    public async Task<string?> WriteAsync(
        PipelineContext pipeline, ISandboxFileReader files, string repository, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(files);
        var path = Path.Combine(Repository.SandboxWorkPath, ProjectMetaPaths.MemoryIndex);
        if (await files.TryReadAsync(path, ct) is not { } index) return null;
        var relabelled = labels.Relabel(index, await standings.OfAsync(pipeline, repository, ct));
        if (string.Equals(relabelled, index, StringComparison.Ordinal)) return null;
        await files.WriteAsync(path, relabelled, ct);
        return ProjectMetaPaths.MemoryIndex;
    }
}
