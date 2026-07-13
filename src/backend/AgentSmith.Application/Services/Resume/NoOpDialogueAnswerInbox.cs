using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Application.Services.Resume;

/// <summary>
/// p0327: DB-free default. Without a relational store there is no durable
/// inbox — answers flow only over the hot stream (pre-p0327 behavior).
/// </summary>
public sealed class NoOpDialogueAnswerInbox : IDialogueAnswerInbox
{
    public Task<bool> TryDeliverAsync(string dialogueJobId, DialogAnswer answer, CancellationToken cancellationToken) =>
        Task.FromResult(false);

    public Task<DialogAnswer?> GetAsync(string dialogueJobId, string questionId, CancellationToken cancellationToken) =>
        Task.FromResult<DialogAnswer?>(null);
}
