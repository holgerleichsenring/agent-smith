namespace AgentSmith.Server.Services.Pocs.Import;

/// <summary>2026-11-23-933db: the caller's PoC project key, or why it could not be created.</summary>
internal sealed record PocProvisioning(string? Project, string? Refusal);
