namespace AgentSmith.Application.Services.Pocs.Acts;

/// <summary>2026-11-23-c81ed: one act as asked — the act (PocActKinds), the revision a rollback rolls to, and an apply's
/// readiness seed (null keeps the row's path, 'tcp' probes the port, a '/' path probes it over HTTP).</summary>
public sealed record PocActOrder(string Act, int? Revision = null, string? Readiness = null)
{
    /// <summary>2026-11-23-c81ee: the lifetime case an expiry or removal run carries out (PocLifetimeCases); null for an
    /// owner's act.</summary>
    public string? LifetimeCase { get; init; }
}
