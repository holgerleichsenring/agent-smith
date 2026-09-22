namespace AgentSmith.Application.Models;

/// <summary>
/// 2026-09-22-7c41a: the spend a run had already made when it parked on a question.
/// Written beside the checkpoint and read once, where the cost tracker is created, so a
/// resumed run's accounting continues the run instead of restarting at zero.
/// <para>
/// RAW buckets, never a weighted total. The token arm of the cost cap re-weights the
/// buckets itself — input, output and cache-create at full weight, cache-read at one
/// tenth — so an already-weighted figure would be weighted a second time and a baseline
/// taken from it would not be comparable with what the segment goes on to spend.
/// </para>
/// <para>
/// The worker-CLI transport travels in its own four buckets for the same reason it is
/// accounted apart: its volume is real context and binds the token arm, while its cost is
/// the CLI's own figure and deliberately binds nothing.
/// </para>
/// </summary>
public sealed record PriorSpendSnapshot(
    int InputTokens,
    int OutputTokens,
    int CacheCreateTokens,
    int CacheReadTokens,
    decimal AccruedUsd,
    long WorkerInputTokens,
    long WorkerOutputTokens,
    long WorkerCacheReadTokens,
    long WorkerCacheCreationTokens,
    decimal WorkerReportedCostUsd,
    string WorkerModels);
