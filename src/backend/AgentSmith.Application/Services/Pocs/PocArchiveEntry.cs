namespace AgentSmith.Application.Services.Pocs;

/// <summary>2026-11-23-933db: one file of an admitted archive as its central directory declares it — the path after
/// the ignore list and the top-folder strip, its CRC-32 and its declared unpacked size.</summary>
public sealed record PocArchiveEntry(string Path, uint Crc32, long Size);
