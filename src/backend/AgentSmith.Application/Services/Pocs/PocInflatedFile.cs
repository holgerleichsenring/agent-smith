namespace AgentSmith.Application.Services.Pocs;

/// <summary>2026-11-23-933db: one file of a PoC archive after the intake inflated it, under its path after the
/// strip.</summary>
internal sealed record PocInflatedFile(string Path, uint Crc32, byte[] Bytes);
