namespace AgentSmith.Server.Services.Pocs.Import;

/// <summary>2026-11-23-933db: what Start posts — new (with a name) or replace (with the PoC), the target, and whether
/// the person confirmed the different-app question.</summary>
internal sealed record PocImportRequest(string? Choice, string? Target, string? Name, long? PocId, bool Confirmed);
