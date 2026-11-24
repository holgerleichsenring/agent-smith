namespace AgentSmith.Application.Services.Archives;

/// <summary>2026-11-23-933db: one entry inflated by <see cref="BoundedZipInflater"/> — its bytes, or the fault that
/// stopped it (then <see cref="Bytes"/> is empty).</summary>
public sealed record ZipInflation(byte[] Bytes, ZipInflationFault? Fault)
{
    public bool IsRefused => Fault is not null;

    public static ZipInflation Refused(ZipInflationFault fault) => new([], fault);
}
