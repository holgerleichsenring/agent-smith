namespace AgentSmith.Contracts.Models.ConfigStudio;

/// <summary>
/// p0345c: editable studio view of one tracker catalog entry — the full raw
/// tracker surface (connection identity + the tracker-owned workflow fields
/// from p0281b + polling cadence). Which identity fields a given
/// <see cref="Type"/> requires is declared by the capabilities descriptor
/// (<c>GET /api/config/capabilities</c>) and enforced on upsert, so the form
/// and the validation cannot drift apart. <see cref="AuthSecret"/> carries the
/// env-NAME of the auth token — never a value. Null collections mean "leave
/// the stored value untouched" on upsert (patch semantics).
/// </summary>
public sealed record TrackerEntity(
    string Id,
    string Type,
    string? AuthSecret,
    string? Url = null,
    string? Organization = null,
    string? Project = null,
    IReadOnlyList<string>? OpenStates = null,
    string? DoneStatus = null,
    string? FailedStatus = null,
    IReadOnlyList<string>? TriggerStatuses = null,
    IReadOnlyDictionary<string, string>? PipelineFromLabel = null,
    TrackerPollingSettings? Polling = null)
{
    public TrackerEntity() : this(string.Empty, string.Empty, null) { }
}

/// <summary>Per-tracker polling cadence (mirrors the raw <c>polling:</c> block).</summary>
public sealed record TrackerPollingSettings(bool Enabled, int IntervalSeconds, int JitterPercent);
