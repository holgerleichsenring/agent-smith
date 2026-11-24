namespace AgentSmith.Server.Services.Pocs.Detail;

/// <summary>2026-11-23-c81ed: GET /api/pocs/{pocId} — the PoC's detail page: its names, host and URL (only while ready),
/// state, desired state, whether a delete is pending (the delete commit landed, its follow has not settled), its owner and
/// last run, which acts the caller may start, the readiness bound, and the audit trail oldest first.</summary>
internal sealed record PocDetailAnswer(
    long Id, string Name, string Project, string Target, string? Host, string? Url, string State, string OwnerName, string? LastRunId,
    PocDesiredView Desired, bool DeletePending, bool MayAct, PocActsView Acts, int ReadinessPathMaxChars, IReadOnlyList<PocAuditView> Audit);
