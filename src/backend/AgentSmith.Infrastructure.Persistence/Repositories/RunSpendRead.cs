namespace AgentSmith.Infrastructure.Persistence.Repositories;

/// <summary>2026-11-24-0fbfb: the runs of a spend window and the llm calls they made.</summary>
public sealed record RunSpendRead(IReadOnlyList<RunSpendRow> Runs, int LlmCalls);
