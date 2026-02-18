namespace AgentSmith.Infrastructure.Providers.Agent;

/// <summary>
/// Result of a scout agent's file discovery pass.
/// Contains relevant files, a context summary, and token usage.
/// </summary>
public sealed record ScoutResult(
    IReadOnlyList<string> RelevantFiles,
    string ContextSummary,
    int TokensUsed);
