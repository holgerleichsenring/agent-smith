using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Surface;

/// <summary>
/// 2026-08-30-c6ec: what the reading of the client sources SAID — its call sites and the
/// files it read and could not decide. The files it read are not in here: that half of the
/// account comes from the tool surface's own read-set, never from a self-report.
/// </summary>
public sealed record ReportedClientUsage(
    IReadOnlyList<ClientCallSite> CallSites,
    IReadOnlyList<UndecidedClientFile> Undecided);
