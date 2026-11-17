namespace AgentSmith.Contracts.Models.Workers;

/// <summary>
/// 2026-11-17-7c9bf: one model's share of a worker CLI call, as the CLI's <c>modelUsage</c> reports it. The
/// call's own model is the one its session ran; any other model the CLI used for itself (Claude Code asks a
/// small model for housekeeping) is secondary usage of that call, never part of its name.
/// </summary>
public sealed record WorkerModelUsage(
    string Model,
    long InputTokens,
    long OutputTokens,
    long CacheReadTokens,
    long CacheCreationTokens,
    decimal CostUsd);
