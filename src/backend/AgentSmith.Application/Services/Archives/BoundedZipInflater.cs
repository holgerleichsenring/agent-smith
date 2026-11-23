using System.IO.Compression;

namespace AgentSmith.Application.Services.Archives;

/// <summary>
/// 2026-11-23-933db: inflates one ZIP entry counting rather than trusting — extracted from the reference upload's
/// reader so the PoC intake holds archives to the same mechanism. The inflated bytes are counted while they are read:
/// an entry's declared length is attacker-supplied, so a lying one stops at the caller's bound, not at what it
/// claimed; one the runtime cut short at its claim fails the CRC-32 the archive records. The bound is the caller's
/// argument, so each reader names its own setting.
/// </summary>
public sealed class BoundedZipInflater(ZipEntryChecksum checksum)
{
    private const int ChunkBytes = 81920; // not a limit: the read buffer

    /// <summary>The entry's bytes, or the fault: over <paramref name="maxBytes"/>, or not matching its checksum.</summary>
    public ZipInflation Inflate(ZipArchiveEntry entry, long maxBytes)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (Counted(entry, maxBytes) is not { } bytes) return ZipInflation.Refused(ZipInflationFault.OverBound);
        return checksum.Of(bytes) == entry.Crc32
            ? new ZipInflation(bytes, null)
            : ZipInflation.Refused(ZipInflationFault.ChecksumMismatch);
    }

    // The inflated bytes, or null as soon as they pass the bound.
    private static byte[]? Counted(ZipArchiveEntry entry, long maxBytes)
    {
        using var source = entry.Open();
        using var into = new MemoryStream();
        var buffer = new byte[ChunkBytes];
        int read;
        while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
        {
            if (into.Length + read > maxBytes) return null;
            into.Write(buffer, 0, read);
        }
        return into.ToArray();
    }
}
