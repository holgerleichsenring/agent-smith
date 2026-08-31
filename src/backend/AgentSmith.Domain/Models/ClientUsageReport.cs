namespace AgentSmith.Domain.Models;

/// <summary>
/// 2026-08-30-c6ec: what the first-party clients were found to exercise, with the account
/// that bounds it. Never a closed set — the account says how far the reading got.
/// </summary>
public sealed record ClientUsageReport(
    IReadOnlyList<ClientCallSite> CallSites,
    ClientExtractionAccount Account);
