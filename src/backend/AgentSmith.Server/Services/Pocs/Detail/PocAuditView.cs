namespace AgentSmith.Server.Services.Pocs.Detail;

/// <summary>2026-11-23-c81ed: one entry of a PoC's audit trail — when, who, what (PocActs), why, and in which run.</summary>
internal sealed record PocAuditView(DateTimeOffset At, string Actor, string Act, string? Reason, string? RunId);
