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
    TrackerPollingSettings? Polling = null,
    // p0392: the rest of the raw tracker's workflow surface. needs_clarification_status is
    // the field the 2026-07-31 outage was about — the server refused to boot without it and
    // it could not be set here, so the way out was a rollback and a hand-edited export.
    string? NeedsClarificationStatus = null,
    string? NotImplementableStatus = null,
    string? CloseTransitionName = null,
    IReadOnlyList<string>? ExtraFields = null,
    bool? ZeroMatchComment = null,
    IReadOnlyDictionary<string, string>? LifecycleStatusNames = null)
{
    public TrackerEntity() : this(string.Empty, string.Empty, null) { }
}

/// <summary>Per-tracker polling cadence (mirrors the raw <c>polling:</c> block).</summary>
public sealed record TrackerPollingSettings(bool Enabled, int IntervalSeconds, int JitterPercent);
