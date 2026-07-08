namespace AgentSmith.Server.Models;

/// <summary>
/// What kind of conversation a state entry tracks: the classic run-job progress
/// channel, or a persistent per-thread spec-dialog design session.
/// </summary>
public enum ConversationMode
{
    RunJob = 0,
    SpecDialog = 1,
}
