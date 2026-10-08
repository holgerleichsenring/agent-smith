namespace AgentSmith.Contracts.Sandbox;

/// <summary>2026-10-08-e8b9k: one image the operator attached, as a run reads it from the store.</summary>
public sealed record ReferenceImageFile(string MediaType, byte[] Content);
