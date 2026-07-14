using AgentSmith.Contracts.Models.Preflight;

namespace AgentSmith.Server.Services.Preflight;

/// <summary>
/// p0324: holds the startup preflight report for the /health endpoint. Null until
/// the warn-only startup run completes — surfaced as "pending", never as a failure.
/// </summary>
internal sealed class PreflightReportStore
{
    private volatile StoredReport? _stored;

    public void Publish(PreflightReport report) =>
        _stored = new StoredReport(report, DateTimeOffset.UtcNow);

    public PreflightReport? Current => _stored?.Report;

    public DateTimeOffset? CompletedAtUtc => _stored?.CompletedAtUtc;

    private sealed record StoredReport(PreflightReport Report, DateTimeOffset CompletedAtUtc);
}
