using AgentSmith.Contracts.Specs;

namespace AgentSmith.Application.Services.Filing;

/// <summary>
/// 2026-11-17-5701b: who approved, where, over which repositories and in which words — what an approval record
/// carries of the conversation it came from. The server builds it from a conversation's state; nothing here reads one.
/// </summary>
/// <param name="Conversation">The design conversation's id, which the record and its citations are keyed by.</param>
/// <param name="Principal">The person who approved.</param>
/// <param name="Repositories">The conversation's scope; empty means the project's own repositories.</param>
/// <param name="OwnerWords">The operator's own messages of the conversation, oldest first.</param>
public sealed record ApprovalSource(
    string Conversation, string Principal, IReadOnlyList<string> Repositories, IReadOnlyList<OwnerWord> OwnerWords);
