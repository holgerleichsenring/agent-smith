namespace AgentSmith.Application.Models;

/// <summary>
/// Discriminator for <see cref="LoopTraceEntry"/>.
/// </summary>
public enum LoopTraceEntryKind
{
    LlmCall,
    ToolCall
}
