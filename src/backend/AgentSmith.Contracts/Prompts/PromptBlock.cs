namespace AgentSmith.Contracts.Prompts;

/// <summary>
/// 2026-11-09-0620a: one unit of what a model call was sent — the tool list, or one message. A block is stored
/// once however many calls send it, so the unit is chosen where calls repeat: a tool loop resends every earlier
/// message and the same tool list on each iteration.
/// </summary>
/// <param name="Role">The message role (system, user, assistant, tool); empty for the tool list.</param>
public sealed record PromptBlock(PromptBlockKind Kind, string Role, string Content);
