namespace AgentSmith.Server.Services.Pocs.Revisions;

/// <summary>2026-11-23-933dc: POST /api/pocs/{pocId}/revisions/{n}/overrule — why the block is lifted.</summary>
internal sealed record PocOverruleRequest(string? Reason);
