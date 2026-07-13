using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Server.Services.Dialogue;

/// <summary>
/// p0327: the server's IDialogueTransport. Answers land in the durable inbox
/// FIRST (survives restarts and the hot stream's 2h TTL — a Friday question
/// answered Monday is no longer lost), then on the hot stream for any
/// in-memory wait. Questions and waits pass straight through to the inner
/// Redis transport; the inbox is additionally consulted before a hot wait so
/// an answer persisted moments earlier (or before a restart) is never missed.
/// </summary>
public sealed class DurableDialogueTransport(
    IDialogueTransport inner,
    IDialogueAnswerInbox inbox) : IDialogueTransport
{
    public Task PublishQuestionAsync(string jobId, DialogQuestion question, CancellationToken cancellationToken) =>
        inner.PublishQuestionAsync(jobId, question, cancellationToken);

    public async Task<DialogAnswer?> WaitForAnswerAsync(
        string jobId, string questionId, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var persisted = await inbox.GetAsync(jobId, questionId, cancellationToken);
        if (persisted is not null) return persisted;
        return await inner.WaitForAnswerAsync(jobId, questionId, timeout, cancellationToken);
    }

    public async Task PublishAnswerAsync(string jobId, DialogAnswer answer, CancellationToken cancellationToken)
    {
        // Durable first — first answer wins; a losing duplicate still flows to
        // the hot stream, where a live wait consumes only the first match anyway.
        await inbox.TryDeliverAsync(jobId, answer, cancellationToken);
        await inner.PublishAnswerAsync(jobId, answer, cancellationToken);
    }
}
