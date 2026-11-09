namespace AgentSmith.Contracts.Prompts;

/// <summary>
/// 2026-11-09-0620a: one model call's prompt in send order.
/// </summary>
/// <param name="Complete">False when part of the call's chain was pruned by retention; the blocks then start
/// where the surviving part starts.</param>
public sealed record PromptRecordView(string Id, int ToolCount, bool Complete, IReadOnlyList<PromptBlockView> Blocks);

/// <param name="Position">The block's place in the call, from 0.</param>
public sealed record PromptBlockView(int Position, string Kind, string Role, int Chars, string Content);
