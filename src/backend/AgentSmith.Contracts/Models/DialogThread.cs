using Microsoft.Extensions.AI;

namespace AgentSmith.Contracts.Models;

/// <summary>
/// The thread a dialog turn continues: the messages the master's previous turn ended with, replayed before this
/// turn's prompt, and the transcript turns added while or since it answered, which the prompt renders.
/// <paramref name="Stewarded"/> names the master whose thread it is; a turn never replays the other master's.
/// </summary>
public sealed record DialogThread(bool Stewarded, IReadOnlyList<ChatMessage> Messages, IReadOnlyList<SpecDialogTurn> Unseen);
