namespace AgentSmith.Application.Services.Pocs;

/// <summary>2026-11-23-933db: one file of a validated revision — its sha256 (hex, lower case), CRC-32 and size.</summary>
public sealed record PocManifestEntry(string Sha256, uint Crc32, long Size);
