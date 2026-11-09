namespace AgentSmith.Infrastructure.Persistence.Entities;

/// <summary>
/// 2026-11-09-0620a: one distinct block of what model calls were sent — a tool list or a message — stored once,
/// keyed by the hash of its kind, role and content. Content is declared by nothing: unbounded, each provider's own
/// convention picks the type.
/// </summary>
public sealed class PromptBlockRow : EntityBase
{
    public string Hash { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public int Chars { get; set; }
    public string Content { get; set; } = string.Empty;
}
