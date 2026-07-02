namespace AgentSmith.Server.Services.Diagnostics;

/// <summary>
/// Initial payload for the dashboard Connections page: the catalog of probeable
/// connections (untested — no outbound calls on load) plus the per-platform
/// webhook panel. Probe results arrive later, one per operator-triggered probe.
/// </summary>
public sealed record ConnectionDiagnosticsSnapshot(
    IReadOnlyList<ConnectionDescriptor> Connections,
    IReadOnlyList<WebhookStatus> Webhooks);
