using AgentSmith.Contracts.Dialogue;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// p0327: the durable answer inbox. Transports write an operator's DialogAnswer
/// here FIRST (survives restarts and the hot stream's TTL), then publish it on
/// the hot stream for any in-memory wait. First answer wins — a duplicate for
/// the same (dialogueJobId, questionId) is dropped by the unique index.
/// </summary>
public interface IDialogueAnswerInbox
{
    /// <summary>Persists the answer. Returns true when this call stored it,
    /// false when an earlier answer already holds the slot (first wins).</summary>
    Task<bool> TryDeliverAsync(string dialogueJobId, DialogAnswer answer, CancellationToken cancellationToken);

    Task<DialogAnswer?> GetAsync(string dialogueJobId, string questionId, CancellationToken cancellationToken);
}
