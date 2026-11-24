namespace AgentSmith.Contracts.Pocs;

/// <summary>
/// 2026-11-23-c81ee: a live PoC as the lifetime pass and the expiry re-check read it — its state and desired state, its
/// lifetime facts, the time of its latest 'expired' audit entry (UTC ticks, null when none), whether the stored upload of
/// its latest revision is gone (read only for a blocked PoC that never deployed, false otherwise) and its last run.
/// </summary>
public sealed record PocLifetimeSubject(
    long PocId, string Name, string Target, string State, int Replicas, int? Revision, PocLifetimeFacts Lifetime,
    long? LatestExpiredAtTicks, bool UploadGone, string? LastRunId);
