namespace AgentSmith.Server.Models;

/// <summary>
/// 2026-11-24-0fbfb: GET /api/runs/spend — what the caller's projects spent on runs started since <see cref="From"/>,
/// in total and apart for work and system runs, with the llm calls and where the money went.
/// </summary>
public sealed record RunSpendView(
    DateTimeOffset From, decimal TotalUsd, RunSpendPart Work, RunSpendPart System, int LlmCalls,
    IReadOnlyList<RunSpendSlice> Slices);
