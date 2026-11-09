namespace AgentSmith.Infrastructure.Persistence.Entities;

/// <summary>
/// 2026-11-09-0620a: what one model call was sent. A call inside a tool loop sends its predecessor's messages plus
/// what came since, so it names that predecessor as its parent, inherits its first <see cref="ParentLength"/>
/// blocks and stores only the rest as its own <see cref="PromptRecordBlock"/> rows.
/// </summary>
public sealed class PromptRecord : EntityBase
{
    public string Id { get; set; } = string.Empty;
    public string RunId { get; set; } = string.Empty;
    public string? ParentId { get; set; }
    public int ParentLength { get; set; }
    public int ToolCount { get; set; }
}
