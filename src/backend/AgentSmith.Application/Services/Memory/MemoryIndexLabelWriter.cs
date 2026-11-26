using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;

namespace AgentSmith.Application.Services.Memory;

/// <summary>
/// 2026-10-25-9324d: the record step's rewrite of the carrier repository's MEMORY.md labels from the evidence —
/// the one writer of a label, so the file in git says what the runs decided. Answers the repository path when
/// the file changed, for the record's staged list; null when there is no index or nothing moved.
/// 2026-11-26-462aa: the memory directory is listed so a line whose file a union merge brought back is dropped.
/// </summary>
public sealed class MemoryIndexLabelWriter(MemoryStandingReader standings, MemoryIndexLabels labels)
{
    public async Task<string?> WriteAsync(
        PipelineContext pipeline, ISandboxFileReader files, string repository, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(files);
        var path = ProjectMetaPaths.MemoryIndex;
        if (await files.TryReadAsync(path, ct) is not { } index) return null;
        var listing = await files.ListAsync(ProjectMetaPaths.Memory, maxDepth: 1, ct);
        // A listing cut at the sandbox's cap names only some files, so it judges nothing.
        var present = listing.Count >= AgentSmith.Sandbox.Wire.SizeLimits.ListFilesMaxEntries ? null
            : listing.Select(p => p.TrimEnd('/')).Select(p => p[(p.LastIndexOf('/') + 1)..]).ToHashSet(StringComparer.Ordinal);
        var relabelled = labels.Relabel(index, await standings.OfAsync(pipeline, repository, ct), present);
        if (string.Equals(relabelled, index, StringComparison.Ordinal)) return null;
        await files.WriteAsync(path, relabelled, ct);
        return ProjectMetaPaths.MemoryIndex;
    }
}
