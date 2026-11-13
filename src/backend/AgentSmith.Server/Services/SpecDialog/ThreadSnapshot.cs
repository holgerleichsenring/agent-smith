using Microsoft.Extensions.AI;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>2026-11-12-92e9a: one thread a running turn offered for writing — 2026-11-12-a1fa: with the transcript
/// indices its master had been delivered mid-turn when it was offered.</summary>
public sealed record ThreadSnapshot(IReadOnlyList<ChatMessage> Messages, bool Stewarded, IReadOnlyList<int> Delivered);
