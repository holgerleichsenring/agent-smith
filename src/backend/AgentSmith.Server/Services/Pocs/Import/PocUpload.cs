namespace AgentSmith.Server.Services.Pocs.Import;

/// <summary>2026-11-23-933db: the one ZIP a PoC upload carried, its file name, and the target field when sent.</summary>
internal sealed record PocUpload(string FileName, byte[] Archive, string? Target);
