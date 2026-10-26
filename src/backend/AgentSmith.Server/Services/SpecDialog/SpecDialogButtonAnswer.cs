using AgentSmith.Contracts.Dialogue;
using AgentSmith.Server.Contracts;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// 2026-10-26-0f19b: a spec-dialog button click that no wait in this process is holding. The thread's
/// open session is read from the store; when the session's open question is the clicked one, the answer
/// goes on the dialogue transport, where the asking replica's confirmation is waiting for it.
/// </summary>
public sealed class SpecDialogButtonAnswer(
    SpecDialogSessionManager sessions, IOpenDialogQuestions openQuestions, IDialogueTransport transport)
{
    public async Task<bool> TryAnswerAsync(
        string platform, string? threadId, DialogAnswer answer, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(threadId)) return false;
        if (await sessions.GetOpenByThreadAsync(platform, threadId, cancellationToken) is not { } session) return false;
        if (!await openQuestions.IsOpenAsync(session.JobId, answer.QuestionId, cancellationToken)) return false;
        await transport.PublishAnswerAsync(session.JobId, answer, cancellationToken);
        return true;
    }
}
