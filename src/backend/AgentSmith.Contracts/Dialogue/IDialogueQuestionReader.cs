namespace AgentSmith.Contracts.Dialogue;

/// <summary>
/// Reads the latest question a run published on its dialogue channel — the question it is
/// asking now, or last asked. Null when the channel holds none (or no longer exists).
/// </summary>
public interface IDialogueQuestionReader
{
    Task<DialogQuestion?> LatestAsync(string dialogueJobId, CancellationToken cancellationToken);
}
