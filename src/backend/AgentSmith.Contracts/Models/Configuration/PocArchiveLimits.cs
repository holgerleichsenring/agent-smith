using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// 2026-11-23-933db: what an uploaded PoC archive is held to, from <c>pocs.archive:</c>. Every refusal names the value,
/// the reason and the key; an absent block reads as these defaults. The two mismatch values steer the different-app
/// guard, which asks before an import replaces a PoC with what looks like another app.
/// </summary>
public sealed class PocArchiveLimits
{
    public const string MaxZipMbKey = "pocs.archive.max_zip_mb";
    public const string MaxUnpackedMbKey = "pocs.archive.max_unpacked_mb";
    public const string MaxEntriesKey = "pocs.archive.max_entries";
    public const string MaxRatioKey = "pocs.archive.max_ratio";
    public const string MaxPathCharsKey = "pocs.archive.max_path_chars";
    public const string MaxFileMbKey = "pocs.archive.max_file_mb";
    public const string MaxBinaryMbKey = "pocs.archive.max_binary_mb";
    public const string MaxDockerfileKbKey = "pocs.archive.max_dockerfile_kb";
    public const string MismatchRatioPercentKey = "pocs.archive.mismatch_ratio_percent";
    public const string MismatchFileLimitKey = "pocs.archive.mismatch_file_limit";

    /// <summary>The uploaded ZIP's size; the upload body is bounded at it.</summary>
    public int MaxZipMb { get; set; } = 25;

    /// <summary>What the archive unpacks to, all files together.</summary>
    public int MaxUnpackedMb { get; set; } = 100;

    /// <summary>How many files the archive may hold.</summary>
    public int MaxEntries { get; set; } = 2000;

    /// <summary>How many times larger than its compressed size a file, and the whole archive, may unpack.</summary>
    public int MaxRatio { get; set; } = 100;

    /// <summary>The longest path inside the archive; at most the store's path column.</summary>
    public int MaxPathChars { get; set; } = ReferenceSetLimits.MaxPathChars;

    /// <summary>One file's unpacked size.</summary>
    public int MaxFileMb { get; set; } = 10;

    /// <summary>The files a NUL byte in their first 8 KB marks as binary, together; checked at intake only.</summary>
    public int MaxBinaryMb { get; set; } = 20;

    /// <summary>The root Dockerfile's size.</summary>
    public int MaxDockerfileKb { get; set; } = 64;

    /// <summary>The different-app guard asks when more than this share of files differs (and the count below too).</summary>
    public int MismatchRatioPercent { get; set; } = 50;

    /// <summary>The different-app guard asks when more than this many files differ (and the share above too).</summary>
    public int MismatchFileLimit { get; set; } = 100;

    /// <summary><paramref name="megabytes"/> in bytes.</summary>
    public static long Megabytes(int megabytes) => megabytes * 1024L * 1024L;

    /// <summary>What is wrong with these values, one sentence each naming the key, the value and the reason; empty when
    /// nothing is. The ONE rule — the load and the settings store refuse what this names.</summary>
    public IEnumerable<string> Problems()
    {
        foreach (var (key, value) in Bounds().Where(v => v.Value < 1))
            yield return $"{key} is {value}; it must be 1 or more, because 0 or less would refuse every PoC archive";
        foreach (var (key, value) in GuardValues().Where(v => v.Value < 1))
            yield return $"{key} is {value}; it must be 1 or more, because 0 or less would ask on every update of a PoC";
        if (MaxPathChars > ReferenceSetLimits.MaxPathChars)
            yield return $"{MaxPathCharsKey} is {MaxPathChars}; it must be at most {ReferenceSetLimits.MaxPathChars}, "
                + "because the stored archive's path column is that wide";
        if (MismatchRatioPercent > 100)
            yield return $"{MismatchRatioPercentKey} is {MismatchRatioPercent}; it must be at most 100, because it is a "
                + "share of the files and above 100 the different-app guard could never ask";
    }

    private IEnumerable<(string Key, int Value)> Bounds() =>
    [
        (MaxZipMbKey, MaxZipMb), (MaxUnpackedMbKey, MaxUnpackedMb), (MaxEntriesKey, MaxEntries), (MaxRatioKey, MaxRatio),
        (MaxPathCharsKey, MaxPathChars), (MaxFileMbKey, MaxFileMb), (MaxBinaryMbKey, MaxBinaryMb),
        (MaxDockerfileKbKey, MaxDockerfileKb),
    ];

    private IEnumerable<(string Key, int Value)> GuardValues() =>
        [(MismatchRatioPercentKey, MismatchRatioPercent), (MismatchFileLimitKey, MismatchFileLimit)];
}
