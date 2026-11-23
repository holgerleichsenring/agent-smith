namespace AgentSmith.Application.Services.Archives;

/// <summary>2026-11-23-933db: why <see cref="BoundedZipInflater"/> handed back no bytes for an entry.</summary>
public enum ZipInflationFault
{
    /// <summary>The inflated bytes passed the caller's bound; reading stopped there.</summary>
    OverBound,

    /// <summary>The inflated bytes do not match the CRC-32 the archive records: damaged, or a lying declared length.</summary>
    ChecksumMismatch,
}
