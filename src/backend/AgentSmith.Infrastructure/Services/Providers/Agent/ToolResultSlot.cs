namespace AgentSmith.Infrastructure.Services.Providers.Agent;

/// <summary>
/// 2026-10-07-6b9dc: where one text tool result sits in a message list — the message index,
/// the content index within it, its call id and its text.
/// </summary>
internal sealed record ToolResultSlot(int Message, int Content, string CallId, string Text);
