namespace AgentSmith.Contracts.Pocs;

/// <summary>2026-11-23-c81ee: the audit act a run records in place of its act's own (<see cref="PocActs"/>) and why —
/// 'expired' for an expiry stop or delete, 'deleted' with the purge's reason for a removal.</summary>
public sealed record PocAuditAct(string Act, string Reason);
