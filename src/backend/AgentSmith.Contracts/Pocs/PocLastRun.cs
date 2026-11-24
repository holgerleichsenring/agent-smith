namespace AgentSmith.Contracts.Pocs;

/// <summary>2026-11-23-c81ee: a run as the lifetime pass's backoff reads it — its preset, status and when it finished
/// (null while it has not).</summary>
public sealed record PocLastRun(string Id, string Pipeline, string Status, DateTimeOffset? FinishedAt);
