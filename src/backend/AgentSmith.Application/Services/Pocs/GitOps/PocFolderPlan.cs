using AgentSmith.Contracts.Pocs;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Pocs.GitOps;

/// <summary>
/// 2026-11-23-c81eb: plans a deploy folder against what the branch holds — each rendered file that differs is written,
/// each file not rendered is deleted. With an unchanged seal the sealed files the PoC needs are kept as the branch holds
/// them (one missing there is reported); otherwise the sealed files are written as the seal step handed them, and a
/// sealed file the PoC no longer needs is deleted like any other. A truncated listing is refused naming the value.
/// </summary>
public sealed class PocFolderPlan
{
    public async Task<(PocFolderChanges? Plan, string? Refusal)> PlanAsync(
        ISourceProvider provider, BranchName branch, string folder, IReadOnlyList<PocRenderedFile> rendered,
        PocSealResult seal, int maxFiles, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(rendered);
        ArgumentNullException.ThrowIfNull(seal);
        var listing = await provider.ListFilesUnderAsync(folder, branch, maxFiles, ct);
        if (listing.Truncated)
            return (null, $"the deploy folder {folder}/ holds more than {maxFiles} files (pocs.gitops.max_folder_files); it holds "
                + "a handful of rendered manifests, so files someone else put there are not planned for deletion");
        var held = listing.Files.ToHashSet(StringComparer.Ordinal);
        var wanted = rendered.Select(f => (Path: $"{folder}/{f.Name}", f.Content))
            .Concat(seal.Unchanged ? [] : seal.Files.Select(f => (Path: $"{folder}/{f.FileName}", f.Content))).ToList();
        var kept = seal.Unchanged ? seal.NeededFiles.Select(f => $"{folder}/{f}").ToList() : [];
        var changes = new List<RepoFile>();
        foreach (var (path, content) in wanted)
            if (!held.Contains(path) || await provider.TryReadFileAsync(path, branch, ct) != content) changes.Add(new RepoFile(path, content));
        var keep = wanted.Select(w => w.Path).Concat(kept).ToHashSet(StringComparer.Ordinal);
        changes.AddRange(listing.Files.Where(p => !keep.Contains(p)).Order(StringComparer.Ordinal).Select(RepoFile.Deletion));
        var missing = kept.Where(p => !held.Contains(p)).Select(p => p[(folder.Length + 1)..]).ToList();
        return (new PocFolderChanges(changes, missing), null);
    }
}
