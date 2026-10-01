namespace AgentSmith.Contracts.Sandbox;

/// <summary>2026-10-01-283dc: one file of an uploaded website — its path inside the set and its bytes.</summary>
public sealed record ReferenceSetFile(string Path, byte[] Content);
