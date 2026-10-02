using System.IO.Compression;
using AgentSmith.Server.Models;

namespace AgentSmith.Server.Services.References;

/// <summary>
/// 2026-10-01-283db: unpacks the ONE archive a set may arrive as, on the server, counting rather
/// than trusting. The entry count is read from the archive's directory before anything is
/// inflated, and the inflated bytes are counted while they are read — an entry's declared length
/// is attacker-supplied, so a lying one stops at the bound, not at what it claimed — and one the
/// runtime cut short at its claim fails its checksum.
/// <para>
/// An archive whose files share no top folder is unpacked under its own name, so the set is
/// named for what the operator dropped rather than called nothing.
/// </para>
/// </summary>
public sealed class ReferenceZipReader(
    ReferencePathRule paths, ReferenceIgnoreList ignored, ZipEntryChecksum checksum)
{
    private const int ChunkBytes = 81920;

    public static bool IsArchive(string path) =>
        path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);

    public ReferenceSetCheck Read(string archiveName, byte[] archive)
    {
        try
        {
            using var zip = new ZipArchive(new MemoryStream(archive), ZipArchiveMode.Read);
            var entries = zip.Entries.Where(e => !e.FullName.EndsWith('/'))
                .Where(e => !ignored.IsIgnored(paths.Normalise(e.FullName))).ToList();
            if (entries.Count > ReferenceUploadLimits.MaxFiles)
                return ReferenceSetCheck.Refused(
                    $"'{archiveName}' holds {entries.Count} files, over the {ReferenceUploadLimits.MaxFiles}-file limit.");
            return Inflate(archiveName, entries);
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
            var bytes = Counted(entry, ReferenceUploadLimits.MaxSetBytes - total);
            if (bytes is null || total + bytes.LongLength > ReferenceUploadLimits.MaxSetBytes)
                return ReferenceSetCheck.Refused(
                    $"'{path}' takes '{archiveName}' over the {ReferenceUploadLimits.Megabytes(ReferenceUploadLimits.MaxSetBytes)} set limit once unpacked.");
            if (checksum.Of(bytes) != entry.Crc32)
                return ReferenceSetCheck.Refused(
                    $"'{path}' in '{archiveName}' does not match its own checksum; the archive is damaged or misstates its size.");
            total += bytes.LongLength;
            files.Add(new ReferenceUploadPart(path, bytes));
        }

        return new ReferenceSetCheck(Rooted(archiveName, files), null);
    }

    // The inflated bytes, or null as soon as they pass what the set has left.
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
        if (root.Length == 0) root = Infrastructure.Persistence.Repositories.ReferenceSetRepository.UnnamedSet;
        return [.. files.Select(f => f with { Path = $"{root}/{f.Path}" })];
    }
}
