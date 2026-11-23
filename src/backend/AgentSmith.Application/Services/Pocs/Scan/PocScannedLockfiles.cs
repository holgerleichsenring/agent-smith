using AgentSmith.Contracts.Pocs;
using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Application.Services.Pocs.Scan;

/// <summary>
/// 2026-11-23-933dc: the copies of a PoC's lockfiles the dependency scan reads — every lockfile by name, a requirements
/// file reduced to its exact pins — and the one note naming what was left out as unpinned.
/// </summary>
public sealed class PocScannedLockfiles(PocLockfiles lockfiles, PocRequirementsFilter requirements, PocFindingIds ids)
{
    public (IReadOnlyList<ReferenceSetFile> Files, PocScanFinding? Unpinned) Select(IReadOnlyList<ReferenceSetFile> files)
    {
        ArgumentNullException.ThrowIfNull(files);
        var scanned = new List<ReferenceSetFile>();
        var leftOut = new List<string>();
        foreach (var file in files.Where(f => lockfiles.IsLockfile(f.Path)))
        {
            if (!lockfiles.IsRequirements(file.Path)) { scanned.Add(file); continue; }
            var (pinned, unpinned) = requirements.Filter(System.Text.Encoding.UTF8.GetString(file.Content));
            leftOut.AddRange(unpinned.Select(line => $"{file.Path}: {line}"));
            if (pinned.Length > 0) scanned.Add(new ReferenceSetFile(file.Path, System.Text.Encoding.UTF8.GetBytes(pinned)));
        }
        return (scanned, leftOut.Count == 0 ? null : Unpinned(leftOut));
    }

    private PocScanFinding Unpinned(IReadOnlyList<string> lines) =>
        new(ids.Note("unpinned|" + string.Join('|', lines)), PocScanFindingKinds.Note, "not-checked-unpinned",
            lines[0][..lines[0].IndexOf(':', StringComparison.Ordinal)], null, "Not checked: unpinned",
            "These requirement lines name no exact version (or are options), so no vulnerability database can say which "
            + "version runs; pin them with '==' to have them checked: " + string.Join("; ", lines), null, null, []);
}
