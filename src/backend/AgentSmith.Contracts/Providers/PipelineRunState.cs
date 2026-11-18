using AgentSmith.Contracts.Reviews;

namespace AgentSmith.Contracts.Providers;

/// <summary>2026-11-18-3863e: a pipeline run as the host reports it — one of <see cref="CiStates"/> (pending while queued
/// or running, green when it succeeded, red when it failed or was cancelled), its page, and a red run's failed jobs.</summary>
public sealed record PipelineRunState(string State, string Url, IReadOnlyList<CiFailure> Failures);
