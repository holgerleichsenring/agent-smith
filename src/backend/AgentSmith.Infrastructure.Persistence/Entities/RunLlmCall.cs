namespace AgentSmith.Infrastructure.Persistence.Entities;

/// <summary>One LLM call's cost + timing record, attributed to a role/phase/model.</summary>
public sealed class RunLlmCall : EntityBase
{
    public long Id { get; set; }
    public string RunId { get; set; } = string.Empty;
    public string? Role { get; set; }
    public string? Phase { get; set; }
    public string? Model { get; set; }
    public long TokensIn { get; set; }
    public long TokensOut { get; set; }
    public decimal CostUsd { get; set; }
    public long DurationMs { get; set; }
    public string? PromptHash { get; set; }
}
