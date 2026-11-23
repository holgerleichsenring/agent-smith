using AgentSmith.Application.Services.Archives;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Services.Pocs;

/// <summary>
/// 2026-11-23-933db: inflates every file of a PoC archive at the intake, each held while it inflates to the smaller of
/// pocs.archive.max_file_mb and what is left of pocs.archive.max_unpacked_mb, whatever it declared, and to its CRC-32.
/// </summary>
public sealed class PocArchiveInflater(BoundedZipInflater inflater)
{
    internal (IReadOnlyList<PocInflatedFile> Files, PocRuleRefusal? Refusal) Inflate(
        IReadOnlyList<PocArchiveItem> items, PocArchiveLimits limits)
    {
        var files = new List<PocInflatedFile>();
        var left = PocArchiveLimits.Megabytes(limits.MaxUnpackedMb);
        foreach (var item in items.Where(i => !i.IsDirectory))
        {
            var perFile = PocArchiveLimits.Megabytes(limits.MaxFileMb);
            var inflated = inflater.Inflate(item.Entry, Math.Min(perFile, left));
            if (inflated.Fault is { } fault)
                return ([], new PocRuleRefusal(PocArchiveChecks.Bounds, Why(fault, item.Path, perFile <= left, limits)));
            left -= inflated.Bytes.LongLength;
            files.Add(new PocInflatedFile(item.Path, item.Entry.Crc32, inflated.Bytes));
        }
        return (files, null);
    }

    private static string Why(ZipInflationFault fault, string path, bool fileBound, PocArchiveLimits limits) =>
        fault != ZipInflationFault.OverBound
            ? $"'{path}' does not match its own checksum; the archive is damaged or misstates its size"
            : fileBound
                ? $"'{path}' unpacks to more than {limits.MaxFileMb} MB, over {PocArchiveLimits.MaxFileMbKey} "
                  + $"{limits.MaxFileMb}, the bound on one file of a PoC's source"
                : $"the archive unpacks to more than {limits.MaxUnpackedMb} MB at '{path}', over "
                  + $"{PocArchiveLimits.MaxUnpackedMbKey} {limits.MaxUnpackedMb}, the bound on what one PoC unpacks to "
                  + "on the server";
}
