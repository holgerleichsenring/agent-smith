namespace AgentSmith.Contracts.Dialogue;

public interface IDialogueTransport
{
    Task PublishQuestionAsync(string jobId, DialogQuestion question, CancellationToken cancellationToken);
    Task<DialogAnswer?> WaitForAnswerAsync(string jobId, string questionId, TimeSpan timeout, CancellationToken cancellationToken);
    Task PublishAnswerAsync(string jobId, DialogAnswer answer, CancellationToken cancellationToken);
}
