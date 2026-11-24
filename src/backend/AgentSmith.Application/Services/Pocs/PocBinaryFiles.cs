using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Application.Services.Pocs;

/// <summary>
/// 2026-11-23-933db: how much of a PoC is binary — a file with a NUL byte in its first 8000 bytes, the way git tells
/// binary from text. Together they may not exceed pocs.archive.max_binary_mb, because the shared source repository
/// keeps every version of them. It needs the inflated content, so it runs at the intake only.
/// </summary>
public sealed class PocBinaryFiles
{
    private const int SniffBytes = 8000; // not a limit: the sniff window, as git reads 8000 bytes

    internal PocRuleRefusal? RefusalOf(IReadOnlyList<PocInflatedFile> files, PocArchiveLimits limits)
    {
        var binaries = files.Where(f => IsBinary(f.Bytes)).OrderByDescending(f => f.Bytes.LongLength).ToList();
        var total = binaries.Sum(f => f.Bytes.LongLength);
        if (total <= PocArchiveLimits.Megabytes(limits.MaxBinaryMb)) return null;
        var largest = string.Join(", ", binaries.Take(3).Select(f => $"'{f.Path}'")); // not a limit: names shown
        return new PocRuleRefusal(PocArchiveChecks.Binary,
            $"the archive holds {ReferenceSetLimits.Megabytes(total)} of binary files (largest {largest}), over "
            + $"{PocArchiveLimits.MaxBinaryMbKey} {limits.MaxBinaryMb}, the bound on binaries a PoC's source may carry, "
            + "because the shared source repository keeps every version of them");
    }

    /// <summary>Whether <paramref name="bytes"/> hold a NUL byte within the sniff window.</summary>
    private static bool IsBinary(byte[] bytes) => bytes.AsSpan(0, Math.Min(bytes.Length, SniffBytes)).IndexOf((byte)0) >= 0;
}
