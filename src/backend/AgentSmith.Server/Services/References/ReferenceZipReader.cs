using System.IO.Compression;
using AgentSmith.Server.Models;

namespace AgentSmith.Server.Services.References;

/// <summary>
/// 2026-10-01-283db: unpacks the ONE archive a set may arrive as, on the server, counting rather
/// than trusting. The inflated bytes are counted while they are read — an entry's declared length
/// is attacker-supplied, so a lying one stops at the bound, not at what it claimed — and one the
/// runtime cut short at its claim fails its checksum. An archive whose files share no top folder
/// is unpacked under its own name.
/// <para>
/// 2026-10-02-075da: <see cref="ReferenceKeepRule"/> decides by path and DECLARED length before
/// anything is inflated, so a .venv inside costs nothing. A kept entry is still held to the
/// per-file bound and the set's while it inflates, whatever it declared. Every entry's path must
/// stay inside the set; the count and size bounds then apply to what was kept.
/// </para>
/// </summary>
public sealed class ReferenceZipReader(ReferencePathRule paths, ReferenceIgnoreList ignored, ZipEntryChecksum checksum)
{
    private const int ChunkBytes = 81920;

    /// <summary>2026-10-09-86e1: the folder an archive named only ".zip" is unpacked under.</summary>
    private const string ArchiveRoot = "archive";

    public static bool IsArchive(string path) =>
        path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);

    public ReferenceSetCheck Read(string archiveName, byte[] archive)
    {
        try
        {
            using var zip = new ZipArchive(new MemoryStream(archive), ZipArchiveMode.Read);
            var candidates = zip.Entries.Where(e => !e.FullName.EndsWith('/'))
                .Select(e => (Entry: e, Path: paths.Normalise(e.FullName))).Where(e => !ignored.IsIgnored(e.Path)).ToList();
            if (candidates.Select(e => paths.HostileRefusalOf(e.Path)).FirstOrDefault(why => why is not null) is { } hostile)
                return ReferenceSetCheck.Refused(hostile);
            var judged = candidates.Select(e => (e.Entry, Why: ReferenceKeepRule.LeftOut(e.Path, e.Entry.Length))).ToList();
            var kept = judged.Where(e => e.Why is null).Select(e => e.Entry).ToList();
            var leftOut = ReferenceKeepRule.Collapsed(judged.Select(e => e.Why).OfType<ReferenceLeftOut>());
            if (kept.Count == 0 && leftOut.Count > 0)
                return ReferenceSetCheck.Refused(ReferenceSetValidator.NothingKept(leftOut));
            if (kept.Count > ReferenceUploadLimits.MaxFiles)
                return ReferenceSetCheck.Refused(
                    $"'{archiveName}' keeps {kept.Count} files, over the {ReferenceUploadLimits.MaxFiles}-file limit.");
            var inflated = Inflate(archiveName, kept);
            return inflated.IsRefused ? inflated : inflated with { LeftOutEntries = leftOut };
        }
        catch (InvalidDataException)
        {
            return ReferenceSetCheck.Refused($"'{archiveName}' is not a readable ZIP archive.");
        }
    }

    private ReferenceSetCheck Inflate(string archiveName, IReadOnlyList<ZipArchiveEntry> entries)
    {
        var files = new List<ReferenceUploadPart>(entries.Count);
        var total = 0L;
        foreach (var entry in entries)
        {
            var path = paths.Normalise(entry.FullName);
            var budget = Math.Min(ReferenceUploadLimits.MaxFileBytes, ReferenceUploadLimits.MaxSetBytes - total);
            if (Counted(entry, budget) is not { } bytes)
                return ReferenceSetCheck.Refused(
                    $"'{path}' takes '{archiveName}' over the {ReferenceUploadLimits.Megabytes(ReferenceUploadLimits.MaxFileBytes)} "
                    + $"per-file or {ReferenceUploadLimits.Megabytes(ReferenceUploadLimits.MaxSetBytes)} set limit once unpacked.");
            if (checksum.Of(bytes) != entry.Crc32)
                return ReferenceSetCheck.Refused(
                    $"'{path}' in '{archiveName}' does not match its own checksum; the archive is damaged or misstates its size.");
            total += bytes.LongLength;
            files.Add(new ReferenceUploadPart(path, bytes));
        }

        return new ReferenceSetCheck(Rooted(archiveName, files), null);
    }

    // The inflated bytes, or null as soon as they pass the budget.
    private static byte[]? Counted(ZipArchiveEntry entry, long budget)
    {
        using var source = entry.Open();
        using var into = new MemoryStream();
        var buffer = new byte[ChunkBytes];
        int read;
        while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
        {
            if (into.Length + read > budget) return null;
            into.Write(buffer, 0, read);
        }
        return into.ToArray();
    }

    private static IReadOnlyList<ReferenceUploadPart> Rooted(string archiveName, List<ReferenceUploadPart> files)
    {
        var tops = files.Select(f => f.Path.IndexOf('/') is var slash and > 0 ? f.Path[..slash] : null).Distinct();
        if (tops.Count() == 1 && tops.Single() is not null) return files;
        var root = Path.GetFileNameWithoutExtension(archiveName.Replace('\\', '/').Split('/')[^1]);
        if (root.Length == 0) root = ArchiveRoot;
        return [.. files.Select(f => f with { Path = $"{root}/{f.Path}" })];
    }
}
