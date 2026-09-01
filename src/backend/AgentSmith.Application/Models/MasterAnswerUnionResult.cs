namespace AgentSmith.Application.Models;

/// <summary>
/// 2026-09-01-0e80: the union of several passes' observation arrays, plus which pass each
/// finding first came from. <see cref="Answer"/> is null when no pass held a readable
/// object literal — the caller then still has the original text, which must stay itself so
/// the findings merge can degrade on it.
/// </summary>
public sealed record MasterAnswerUnionResult(
    string? Answer,
    IReadOnlyDictionary<string, string> Origins);
