namespace AgentSmith.Server.Models;

/// <summary>2026-10-01-283db: one file of an uploaded website — its path inside the set, and its bytes.</summary>
public sealed record ReferenceUploadPart(string Path, byte[] Content);
