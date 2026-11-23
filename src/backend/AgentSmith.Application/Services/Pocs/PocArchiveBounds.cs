using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Application.Services.Pocs;

/// <summary>
/// 2026-11-23-933db: the size bounds of pocs.archive — the ZIP itself, the file count, one file, everything unpacked,
/// and how many times its compressed size a file and the whole archive unpack to, in that order. At the upload they read the sizes
/// the central directory declares; at the intake the sizes actually inflated. Every refusal names the value, the key
/// and why the bound exists.
/// </summary>
public sealed class PocArchiveBounds
{
    private const string BombReason = "the bound that stops a ZIP bomb before it fills the server's memory";

    internal PocRuleRefusal? RefusalOf(IReadOnlyList<PocArchiveItem> items, long archiveBytes, PocArchiveLimits limits)
    {
        var files = items.Where(i => !i.IsDirectory).ToList();
        var unpacked = files.Sum(f => f.Size);
        var why = ZipSize(archiveBytes, limits) ?? Count(files.Count, limits)
            ?? files.Select(f => FileSize(f, limits)).FirstOrDefault(r => r is not null) ?? Unpacked(unpacked, limits)
            ?? files.Select(f => Ratio($"'{f.Path}'", f.Size, f.CompressedSize, limits)).FirstOrDefault(r => r is not null)
            ?? Ratio("the archive", unpacked, archiveBytes, limits);
        return why is null ? null : new PocRuleRefusal(PocArchiveChecks.Bounds, why);
    }

    private static string? ZipSize(long bytes, PocArchiveLimits limits) =>
        bytes <= PocArchiveLimits.Megabytes(limits.MaxZipMb) ? null
            : $"the archive is {Mb(bytes)}, over {PocArchiveLimits.MaxZipMbKey} {limits.MaxZipMb}, the bound on an "
              + "upload the server reads into memory";

    private static string? Count(int count, PocArchiveLimits limits) =>
        count <= limits.MaxEntries ? null
            : $"the archive holds {count} files, over {PocArchiveLimits.MaxEntriesKey} {limits.MaxEntries}, the bound on "
              + "how many files one PoC may carry";

    private static string? FileSize(PocArchiveItem file, PocArchiveLimits limits) =>
        file.Size <= PocArchiveLimits.Megabytes(limits.MaxFileMb) ? null
            : $"'{file.Path}' unpacks to {Mb(file.Size)}, over {PocArchiveLimits.MaxFileMbKey} {limits.MaxFileMb}, the "
              + "bound on one file of a PoC's source";

    private static string? Unpacked(long bytes, PocArchiveLimits limits) =>
        bytes <= PocArchiveLimits.Megabytes(limits.MaxUnpackedMb) ? null
            : $"the archive unpacks to {Mb(bytes)}, over {PocArchiveLimits.MaxUnpackedMbKey} {limits.MaxUnpackedMb}, the "
              + "bound on what one PoC unpacks to on the server";

    // Over the ratio when the unpacked size exceeds max_ratio times the compressed one; shown rounded up, so a
    // refused ratio never reads as the bound itself.
    private static string? Ratio(string what, long unpacked, long compressed, PocArchiveLimits limits)
    {
        if (unpacked <= (long)limits.MaxRatio * compressed) return null;
        var ratio = compressed == 0 ? unpacked : (unpacked + compressed - 1) / compressed;
        return $"{what} unpacks {ratio}:1, over {PocArchiveLimits.MaxRatioKey} {limits.MaxRatio}, {BombReason}";
    }

    private static string Mb(long bytes) => ReferenceSetLimits.Megabytes(bytes);
}
