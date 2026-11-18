namespace AgentSmith.Infrastructure.Persistence.Services.Secrets;

/// <summary>2026-11-17-f3a8d: a master key — its 32 bytes, and the id rows record it by, which reveals nothing of it.</summary>
public sealed record MasterKey(string Id, byte[] Key);
