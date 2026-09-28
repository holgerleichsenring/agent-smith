using AgentSmith.Contracts.Models;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// The durable binding of chat-started runs to their threads. Every replica reads the open
/// bindings, so the two writes that decide who speaks — posting a question and posting the
/// outcome — are conditional: exactly one caller wins each, and only the winner posts.
/// </summary>
public interface IChatRunBindingStore
{
    Task BindAsync(ChatRunBindingFact binding, CancellationToken cancellationToken);

    Task<IReadOnlyList<ChatRunBindingFact>> ListOpenAsync(CancellationToken cancellationToken);

    /// <summary>The open binding of a thread, or null when no run started there is still open.</summary>
    Task<ChatRunBindingFact?> FindOpenInThreadAsync(
        string platform, string channelId, string? threadId, CancellationToken cancellationToken);

    /// <summary>Records the question as the one posted to the thread. False when it already is,
    /// or when the binding is closed — the caller then posts nothing.</summary>
    Task<bool> TryRecordQuestionAsync(
        string runId, string questionId, string questionJson, CancellationToken cancellationToken);

    /// <summary>Closes the binding. True for exactly one caller — the one that posts the outcome.</summary>
    Task<bool> TryCloseAsync(string runId, CancellationToken cancellationToken);
}
