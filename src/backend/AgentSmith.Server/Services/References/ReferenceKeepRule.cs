using AgentSmith.Server.Models;

namespace AgentSmith.Server.Services.References;

/// <summary>
/// 2026-10-02-075da: whether one file of an upload is stored, decided by the ABSENCE of a reason
/// to leave it out — never by its type. A file is left out when it lies under a rebuildable folder
/// or is over the per-file bound; a rebuildable folder is named once, as the folder.
/// </summary>
public static class ReferenceKeepRule
{
    /// <summary>Why the file at <paramref name="path"/> of <paramref name="bytes"/> is left out, or null when it is kept.</summary>
    public static ReferenceLeftOut? LeftOut(string path, long bytes)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (ReferenceRebuildableFolders.FolderOf(path) is { } folder)
            return new ReferenceLeftOut(folder, ReferenceLeftOut.Rebuildable);
        return bytes > ReferenceUploadLimits.MaxFileBytes
            ? new ReferenceLeftOut(path, $"{ReferenceLeftOut.TooLarge}: {ReferenceUploadLimits.Megabytes(bytes)}, over "
                + $"the {ReferenceUploadLimits.Megabytes(ReferenceUploadLimits.MaxFileBytes)} per-file limit")
            : null;
    }

    /// <summary>The left-out entries, each rebuildable folder once, in path order.</summary>
    public static IReadOnlyList<ReferenceLeftOut> Collapsed(IEnumerable<ReferenceLeftOut> entries) =>
        [.. entries.DistinctBy(e => e.Path, StringComparer.Ordinal).OrderBy(e => e.Path, StringComparer.Ordinal)];
}
