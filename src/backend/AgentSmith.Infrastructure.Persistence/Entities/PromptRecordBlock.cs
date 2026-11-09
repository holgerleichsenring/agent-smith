namespace AgentSmith.Infrastructure.Persistence.Entities;

/// <summary>2026-11-09-0620a: a block a record holds itself, at its position in the call (from 0).</summary>
public sealed class PromptRecordBlock
{
    public string RecordId { get; set; } = string.Empty;
    public int Position { get; set; }
    public string BlockHash { get; set; } = string.Empty;
}
