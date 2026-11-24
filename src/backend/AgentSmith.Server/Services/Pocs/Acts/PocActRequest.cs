namespace AgentSmith.Server.Services.Pocs.Acts;

/// <summary>2026-11-23-c81ed: POST /api/pocs/{pocId}/acts/{act} — the revision a rollback rolls to, the name a delete is
/// confirmed with, and an apply's readiness (absent keeps the path, 'tcp' probes the port, a '/' path probes it).</summary>
internal sealed record PocActRequest(int? Revision = null, string? ConfirmName = null, string? Readiness = null);
